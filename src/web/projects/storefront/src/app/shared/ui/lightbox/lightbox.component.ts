import { ChangeDetectionStrategy, Component, ElementRef, computed, input, signal, viewChild } from '@angular/core';
import { RpDialog } from '../../../layout/dialog/dialog.directive';
import { INITIAL_ZOOM, Box, Point, ZoomState, panBy, pinchTo, stepZoom, toggleAt } from '../../utils/zoom.util';
import { IconComponent } from '../icon/icon.component';

export interface LightboxImage {
  readonly alt: string;
  readonly width: number;
  readonly height: number;
  /** The largest WebP the pipeline made for this picture. Requested only when the viewer shows it (GAL-9). */
  readonly src: string;
  readonly fallback: string | null;
}

const SWIPE_DISTANCE = 50;
const TAP_SLOP = 6;
const DOUBLE_TAP_MS = 300;
const KEY_PAN = 40;

/**
 * Full-screen picture viewer (design system: Lightbox; GAL-3..10). A native `<dialog>` opened with `showModal()` through `RpDialog`, which
 * gives the inert background, the focus trap, Esc, the scroll lock and the focus return. Zoom and pan maths are in `zoom.util`; this
 * component only translates pointers and keys into those calls. Every gesture has a button or key (WCAG 2.5.7).
 */
@Component({
  selector: 'rp-lightbox',
  imports: [RpDialog, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './lightbox.component.html',
})
export class LightboxComponent {
  readonly images = input.required<readonly LightboxImage[]>();

  private readonly dialog = viewChild.required(RpDialog);
  private readonly frame = viewChild<ElementRef<HTMLElement>>('frame');

  protected readonly index = signal<number | null>(null);
  protected readonly zoom = signal<ZoomState>(INITIAL_ZOOM);
  protected readonly current = computed(() => {
    const i = this.index();
    return i === null ? null : (this.images()[i] ?? null);
  });
  protected readonly count = computed(() => this.images().length);
  protected readonly isZoomed = computed(() => this.zoom().scale > 1);
  protected readonly transform = computed(() => {
    const z = this.zoom();
    return `translate(${z.x}px, ${z.y}px) scale(${z.scale})`;
  });

  private readonly pointers = new Map<number, Point>();
  private pinch: { distance: number; scale: number } | null = null;
  private dragFrom: Point | null = null;
  private moved = 0;
  private swipeDx = 0;
  private lastTap = 0;

  open(start: number): void {
    this.index.set(Math.min(Math.max(0, start), Math.max(0, this.images().length - 1)));
    this.zoom.set(INITIAL_ZOOM);
    this.dialog().open();
  }

  close(): void {
    this.dialog().close();
  }

  protected onClosed(): void {
    this.index.set(null);
    this.zoom.set(INITIAL_ZOOM);
    this.pointers.clear();
    this.pinch = null;
    this.dragFrom = null;
  }

  protected move(step: 1 | -1): void {
    const i = this.index();
    if (i === null) return;
    const next = i + step;
    if (next < 0 || next >= this.count()) return;
    this.index.set(next);
    this.zoom.set(INITIAL_ZOOM);
  }

  protected zoomBy(direction: 1 | -1): void {
    this.zoom.update((z) => stepZoom(z, direction, this.box()));
  }

  protected reset(): void {
    this.zoom.set(INITIAL_ZOOM);
  }

  protected onKey(event: KeyboardEvent): void {
    if (this.index() === null) return;
    const zoomed = this.isZoomed();
    switch (event.key) {
      case 'ArrowRight':
        if (zoomed) this.panKey(-KEY_PAN, 0);
        else this.move(1);
        break;
      case 'ArrowLeft':
        if (zoomed) this.panKey(KEY_PAN, 0);
        else this.move(-1);
        break;
      case 'ArrowUp':
        if (zoomed) this.panKey(0, KEY_PAN);
        else return;
        break;
      case 'ArrowDown':
        if (zoomed) this.panKey(0, -KEY_PAN);
        else return;
        break;
      case '+':
      case '=':
        this.zoom.update((z) => toggleAt(z, { x: 0, y: 0 }, this.box()));
        break;
      case '-':
      case '_':
        this.zoomBy(-1);
        break;
      default:
        return;
    }
    event.preventDefault();
  }

  protected onPointerDown(event: PointerEvent): void {
    if (event.button !== undefined && event.button !== 0) return;
    this.pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    this.capture(event, true);
    if (this.pointers.size === 1) {
      this.dragFrom = { x: event.clientX, y: event.clientY };
      this.moved = 0;
      this.swipeDx = 0;
    } else if (this.pointers.size === 2) {
      this.pinch = { distance: this.distance(), scale: this.zoom().scale };
      this.dragFrom = null;
    }
  }

  protected onPointerMove(event: PointerEvent): void {
    if (!this.pointers.has(event.pointerId)) return;
    const prev = this.pointers.get(event.pointerId)!;
    this.pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    if (this.pointers.size >= 2 && this.pinch) {
      const centre = this.fromCentre(this.midpoint());
      this.zoom.update((z) => pinchTo(z, this.pinch!.scale, this.pinch!.distance, this.distance(), centre, this.box()));
      this.moved = TAP_SLOP + 1;
      return;
    }
    if (!this.dragFrom) return;
    const dx = event.clientX - prev.x;
    const dy = event.clientY - prev.y;
    this.moved += Math.abs(dx) + Math.abs(dy);
    if (this.isZoomed()) this.zoom.update((z) => panBy(z, dx, dy, this.box()));
    else this.swipeDx = event.clientX - this.dragFrom.x;
  }

  protected onPointerUp(event: PointerEvent): void {
    if (!this.pointers.has(event.pointerId)) return;
    const wasPinch = this.pointers.size >= 2;
    this.pointers.delete(event.pointerId);
    this.capture(event, false);
    if (wasPinch) {
      this.pinch = null;
      this.dragFrom = null;
      return;
    }
    if (this.moved <= TAP_SLOP) this.tap(event);
    else if (!this.isZoomed() && Math.abs(this.swipeDx) >= SWIPE_DISTANCE) this.move(this.swipeDx < 0 ? 1 : -1);
    this.dragFrom = null;
    this.swipeDx = 0;
  }

  protected onPointerCancel(event: PointerEvent): void {
    this.pointers.delete(event.pointerId);
    this.pinch = null;
    this.dragFrom = null;
  }

  /** A mouse click toggles; on touch the double-tap does, so a single tap never zooms by accident. */
  private tap(event: PointerEvent): void {
    const point = this.fromCentre({ x: event.clientX, y: event.clientY });
    if (event.pointerType === 'mouse' || event.pointerType === 'pen') {
      this.zoom.update((z) => toggleAt(z, point, this.box()));
      return;
    }
    const now = Date.now();
    if (now - this.lastTap < DOUBLE_TAP_MS) {
      this.zoom.update((z) => toggleAt(z, point, this.box()));
      this.lastTap = 0;
    } else {
      this.lastTap = now;
    }
  }

  private capture(event: PointerEvent, on: boolean): void {
    const el = event.currentTarget as HTMLElement | null;
    try {
      if (on) el?.setPointerCapture?.(event.pointerId);
      else el?.releasePointerCapture?.(event.pointerId);
    } catch {
      // A pointer that is already gone cannot be captured; nothing to do.
    }
  }

  private box(): Box {
    const r = this.frame()?.nativeElement.getBoundingClientRect();
    return { width: r?.width ?? 0, height: r?.height ?? 0 };
  }

  private fromCentre(p: Point): Point {
    const r = this.frame()?.nativeElement.getBoundingClientRect();
    if (!r) return p;
    return { x: p.x - (r.left + r.width / 2), y: p.y - (r.top + r.height / 2) };
  }

  private midpoint(): Point {
    const [a, b] = [...this.pointers.values()];
    return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
  }

  private distance(): number {
    const [a, b] = [...this.pointers.values()];
    return Math.hypot(a.x - b.x, a.y - b.y);
  }

  private panKey(dx: number, dy: number): void {
    this.zoom.update((z) => panBy(z, dx, dy, this.box()));
  }
}

