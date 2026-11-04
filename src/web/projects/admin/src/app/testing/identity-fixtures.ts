import type { RoleDto, StaffDto, TemporaryPasswordDto } from '@features/identity/staff-accounts';

export const staffMember = (overrides: Partial<StaffDto> = {}): StaffDto => ({
  id: 11,
  userName: 'amna.khan',
  displayName: 'Amna Khan',
  isActive: true,
  mustChangePassword: false,
  hasTemporaryPassword: false,
  temporaryPasswordExpiresAt: null,
  roles: [{ id: 2, code: 'CATALOGUE', name: 'Catalogue' }],
  createdAt: '2026-10-20T09:00:00Z',
  ...overrides,
});

export const roleOption = (overrides: Partial<RoleDto> = {}): RoleDto => ({
  id: 2,
  code: 'CATALOGUE',
  name: 'Catalogue',
  description: null,
  hasAllPermissions: false,
  ...overrides,
});

export const temporaryPassword = (overrides: Partial<TemporaryPasswordDto> = {}): TemporaryPasswordDto => ({
  userId: 11,
  userName: 'amna.khan',
  temporaryPassword: 'Xk7m-Qp4t-Rv9w-Zc2n',
  expiresAt: '2026-10-28T09:00:00Z',
  ...overrides,
});
