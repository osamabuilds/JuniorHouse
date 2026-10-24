/** One product or editorial image. `blurDataUrl` is a ~20px data URL shown blurred until the file loads. */
export interface ImageAsset {
  readonly src: string;
  readonly width: number;
  readonly height: number;
  readonly alt: string;
  readonly blurDataUrl?: string;
}
