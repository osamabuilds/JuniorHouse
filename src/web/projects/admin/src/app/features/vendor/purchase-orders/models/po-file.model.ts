export interface PoFileDto {
  readonly id: number;
  readonly fileName: string;
  readonly categoryId: number;
  readonly isVendorVisible: boolean;
  readonly fileSizeBytes: number;
  readonly uploadedBy: string;
  readonly uploadedAt: string;
  readonly addedInRevisionNumber: number | null;
  readonly retiredInRevisionNumber: number | null;
}

/** A file picked in the Files panel, ready to upload. */
export interface PoFileUpload {
  readonly categoryId: number;
  readonly file: File;
}
