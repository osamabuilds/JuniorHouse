/** The combined shipping label (LBL-1): recipient, full address and phone, the courier and waybill with its barcode, and the cash to collect or Paid. Staff only. */
export interface ShippingLabel {
  readonly shipmentId: number;
  readonly orderNumber: string;
  readonly courierName: string;
  readonly waybill: string;
  /** A string of 1 (bar) and 0 (space) modules for a Code 128 barcode, without the quiet zone (LBL-3). */
  readonly barcodeModules: string;
  readonly recipientName: string;
  readonly phone: string;
  readonly addressLine1: string;
  readonly addressLine2: string | null;
  readonly city: string;
  readonly province: string;
  readonly postalCode: string | null;
  readonly deliveryNote: string | null;
  readonly isCashOnDelivery: boolean;
  readonly cashToCollect: number;
  readonly weightGrams: number;
  readonly isReprint: boolean;
  readonly printCount: number;
}

export interface BarRun {
  readonly x: number;
  readonly width: number;
}

/** Turns the module string into the bars to draw: runs of 1s, with their start and width in modules (LBL-3). */
export function barRuns(modules: string): BarRun[] {
  const runs: BarRun[] = [];
  let start = -1;
  for (let i = 0; i <= modules.length; i++) {
    const bar = i < modules.length && modules[i] === '1';
    if (bar && start < 0) {
      start = i;
    } else if (!bar && start >= 0) {
      runs.push({ x: start, width: i - start });
      start = -1;
    }
  }

  return runs;
}
