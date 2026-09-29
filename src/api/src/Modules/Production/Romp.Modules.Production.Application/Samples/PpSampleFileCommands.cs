using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Application.ProductionRuns;
using Romp.Modules.Production.Domain.ProductionRuns;

namespace Romp.Modules.Production.Application.Samples;

/// <summary>D7 / AC-12: attach a photo or file to a sample round, reusing the shared storage and its content-signature check (never the client's filename or content type).</summary>
public sealed record UploadPpSampleFileCommand(long RunId, int RoundNo, string FileName, byte[] Content) : IRequest<PpSampleFileDto>, IProductionCommand;

public sealed class UploadPpSampleFileCommandValidator : AbstractValidator<UploadPpSampleFileCommand>
{
    public UploadPpSampleFileCommandValidator(ProductionOptions options)
    {
        RuleFor(c => c.FileName).NotEmpty().WithMessage("The file needs a name.");
        RuleFor(c => c.Content)
            .Must(content => content.Length > 0).WithMessage("The file is empty.")
            .Must(content => content.LongLength <= options.MaxFileSizeBytes)
            .WithMessage($"The file is larger than the {options.MaxFileSizeBytes / (1024 * 1024)} MB limit.")
            .Must(content => content.Length == 0 || FileContent.DetectContentType(content) is not null)
            .WithMessage($"This file type isn't allowed. Upload a {FileContent.AllowedTypesDescription} file.");
    }
}

public sealed class UploadPpSampleFileCommandHandler(IProductionDbContext dbContext, IFileStorage fileStorage, ProductionOptions options)
    : IRequestHandler<UploadPpSampleFileCommand, PpSampleFileDto>
{
    public async Task<PpSampleFileDto> Handle(UploadPpSampleFileCommand request, CancellationToken cancellationToken)
    {
        var run = await dbContext.LoadRunAsync(request.RunId, cancellationToken);
        if (run.StatusId != RunStatus.Open)
        {
            throw new DomainException($"The production run for PO {run.PoNo} is not open, so files cannot be added.");
        }

        var round = run.PpRounds.FirstOrDefault(r => r.RoundNo == request.RoundNo)
            ?? throw new KeyNotFoundException($"Sample round {request.RoundNo} was not found on the production run for PO {run.PoNo}.");

        if (round.Files.Count(f => !f.IsDeleted) >= options.MaxFilesPerRound)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Content)] = [$"Round {round.RoundNo} already has the maximum of {options.MaxFilesPerRound} files."],
            });
        }

        // Validation has already passed, so the type is known. The bytes are stored only after every check.
        var contentType = FileContent.DetectContentType(request.Content)!;
        var storageKey = await fileStorage.SaveAsync(request.Content, cancellationToken);
        var file = round.AddFile(storageKey, FileContent.SanitiseFileName(request.FileName), contentType, request.Content.LongLength);

        await dbContext.SaveChangesAsync(cancellationToken);
        return file.ToDto();
    }
}

/// <summary>Soft-removes a file added by mistake; the bytes stay in storage.</summary>
public sealed record RemovePpSampleFileCommand(long RunId, int RoundNo, long FileId) : IRequest, IProductionCommand;

public sealed class RemovePpSampleFileCommandHandler(IProductionDbContext dbContext) : IRequestHandler<RemovePpSampleFileCommand>
{
    public async Task Handle(RemovePpSampleFileCommand request, CancellationToken cancellationToken)
    {
        var file = await dbContext.PpFiles
            .Where(f => f.Id == request.FileId && !f.IsDeleted)
            .Where(f => dbContext.PpRounds.Any(r => r.Id == f.RoundId && r.RunId == request.RunId && r.RoundNo == request.RoundNo))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"File {request.FileId} was not found on round {request.RoundNo}.");

        file.SoftDelete();
    }
}

/// <summary>AC-12: download. The headers force a download and stop the browser guessing a type; the endpoint applies them verbatim.</summary>
public sealed record GetPpSampleFileQuery(long RunId, int RoundNo, long FileId) : IRequest<PpSampleFileDownload>;

public sealed record PpSampleFileDownload(byte[] Content, string ContentType, string FileName, IReadOnlyDictionary<string, string> Headers);

public sealed class GetPpSampleFileQueryHandler(IProductionDbContext dbContext, IFileStorage fileStorage)
    : IRequestHandler<GetPpSampleFileQuery, PpSampleFileDownload>
{
    public async Task<PpSampleFileDownload> Handle(GetPpSampleFileQuery request, CancellationToken cancellationToken)
    {
        var file = await dbContext.PpFiles.AsNoTracking()
            .Where(f => f.Id == request.FileId && !f.IsDeleted)
            .Where(f => dbContext.PpRounds.Any(r => r.Id == f.RoundId && r.RunId == request.RunId && r.RoundNo == request.RoundNo))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"File {request.FileId} was not found on round {request.RoundNo}.");

        var content = await fileStorage.RetrieveAsync(file.StorageKey, cancellationToken);
        var safeName = FileContent.SanitiseFileName(file.FileName);

        return new PpSampleFileDownload(content, file.ContentType, safeName, new Dictionary<string, string>
        {
            ["Content-Disposition"] = $"attachment; filename=\"{safeName}\"",
            ["X-Content-Type-Options"] = "nosniff",
        });
    }
}
