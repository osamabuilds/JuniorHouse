import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import QRCode from 'qrcode';

/** Four modules of empty space on every side: the quiet zone a scanner needs to find the code (ISO/IEC 18004). */
const QUIET_ZONE = 4;

/**
 * Draws a QR code as inline SVG, in the browser (mfa plan: no third-party QR service ever sees the secret). The pattern is computed
 * from the text and drawn as one path, so nothing is loaded and no HTML is injected. Black on white whatever the page theme, because a
 * scanner needs the contrast. The text alternative is supplied by the caller (the secret itself is shown beside it as text).
 */
@Component({
  selector: 'app-qr-code',
  template: `
    @if (view(); as v) {
      <svg class="qr" role="img" [attr.aria-label]="label()" [attr.viewBox]="v.viewBox" shape-rendering="crispEdges">
        <rect [attr.width]="v.size" [attr.height]="v.size" fill="#ffffff" />
        <path [attr.d]="v.path" fill="#000000" />
      </svg>
    }
  `,
  styles: ':host { display: block; } .qr { display: block; width: 14rem; max-width: 100%; height: auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QrCodeComponent {
  readonly text = input<string | null>(null);
  readonly label = input('QR code');

  readonly view = computed(() => {
    const text = this.text();
    if (!text) {
      return null;
    }

    const { modules } = QRCode.create(text, { errorCorrectionLevel: 'M' });
    const size = modules.size + QUIET_ZONE * 2;
    let path = '';
    for (let row = 0; row < modules.size; row++) {
      for (let column = 0; column < modules.size; column++) {
        if (modules.get(row, column)) {
          path += `M${column + QUIET_ZONE} ${row + QUIET_ZONE}h1v1h-1z`;
        }
      }
    }

    return { size, viewBox: `0 0 ${size} ${size}`, path };
  });
}
