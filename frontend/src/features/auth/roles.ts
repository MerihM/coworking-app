// Role names exactly as the API sends them (Coworking.Application.Auth.Roles on the backend).
export const Roles = {
  Member: "Member",
  Receptionist: "Receptionist",
  Manager: "Manager",
  Administrator: "Administrator",
} as const;

export type Role = (typeof Roles)[keyof typeof Roles];

/** Translation key for each role, for labels like "Uloga: Recepcioner". */
export const roleLabelKeys = {
  Member: "roles.member",
  Receptionist: "roles.receptionist",
  Manager: "roles.manager",
  Administrator: "roles.administrator",
} as const satisfies Record<Role, string>;

/** Narrows the plain string from the API to a known Role. */
export function isRole(value: string): value is Role {
  return Object.values(Roles).some((role) => role === value);
}
