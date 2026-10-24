/** Test helper: jsdom has no modal <dialog>, so model the parts RpDialog relies on. */
export function polyfillDialog(): void {
  const proto = HTMLDialogElement.prototype as unknown as Record<string, unknown>;
  proto['showModal'] = function (this: HTMLDialogElement) { this.setAttribute('open', ''); };
  proto['close'] = function (this: HTMLDialogElement) { this.removeAttribute('open'); this.dispatchEvent(new Event('close')); };
}
