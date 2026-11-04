/** A role an account holds (GET /api/identity/staff). */
export interface StaffRoleDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
}

/** An account as the back office sees it. It never carries a password, a hash or a temporary password. */
export interface StaffDto {
  readonly id: number;
  readonly userName: string;
  readonly displayName: string;
  readonly isActive: boolean;
  readonly mustChangePassword: boolean;
  readonly hasTemporaryPassword: boolean;
  readonly temporaryPasswordExpiresAt: string | null;
  readonly roles: readonly StaffRoleDto[];
  readonly createdAt: string;
}

/** A role that can be given to a person (GET /api/identity/roles). */
export interface RoleDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly description: string | null;
  readonly hasAllPermissions: boolean;
}

export interface StaffFilters {
  readonly searchText: string;
  readonly activeOnly: boolean;
  readonly page: number;
  readonly pageSize: number;
}

export interface CreateStaffValue {
  readonly userName: string;
  readonly displayName: string;
  readonly roleIds: readonly number[];
}

/** The one response that carries a generated password. It is shown once and then dropped from the store. */
export interface TemporaryPasswordDto {
  readonly userId: number;
  readonly userName: string;
  readonly temporaryPassword: string;
  readonly expiresAt: string;
}
