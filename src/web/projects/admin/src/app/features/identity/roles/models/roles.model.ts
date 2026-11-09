/** A permission as the matrix lists it (GET /api/identity/roles/matrix). */
export interface PermissionDto {
  readonly code: string;
  readonly name: string;
  readonly description: string | null;
}

/** A role and the permission codes it holds; for the Owner (the all-permissions flag) that is every permission. */
export interface RoleWithPermissionsDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly description: string | null;
  readonly hasAllPermissions: boolean;
  readonly permissionCodes: readonly string[];
}

export interface RoleMatrixDto {
  readonly permissions: readonly PermissionDto[];
  readonly roles: readonly RoleWithPermissionsDto[];
}
