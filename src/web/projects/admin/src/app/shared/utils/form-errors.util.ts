import { AbstractControl } from '@angular/forms';

/** "Target unit cost (PKR)" -> "target unit cost". */
export function plainName(label: string): string {
  return label.replace(/\s*\(.*?\)/g, '').trim().toLowerCase();
}

/**
 * A single sentence naming every required field that is still empty or invalid, for the banner
 * shown when someone tries to save an incomplete form. `labels` maps a control name to the words
 * staff see on screen; `extra` adds non-control requirements such as "at least one colour".
 */
export function missingSummary(
  controls: Readonly<Record<string, AbstractControl | null>>,
  labels: Readonly<Record<string, string>>,
  extra: readonly string[] = [],
): string {
  const missing = [
    ...Object.entries(labels)
      .filter(([key]) => controls[key]?.invalid)
      .map(([, label]) => plainName(label)),
    ...extra,
  ];
  return missing.length > 0
    ? `This can't be saved yet. Still needed: ${missing.join(', ')}.`
    : "This can't be saved yet. Please check the fields marked in red.";
}
