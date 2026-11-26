// Form rules for workspaces – the same as CreateWorkspaceRequestValidator / Update... on the backend.
import type { TFunction } from "i18next";
import { z } from "zod";
import {
  capacityRange,
  normalizeCode,
  workspaceCodePattern,
  workspaceTypes,
} from "./workspaceTypes";

export const codeMax = 20;

/** Create and edit share the fields; capacity is checked against the CHOSEN type. */
export function workspaceSchema(t: TFunction) {
  return z
    .object({
      roomId: z
        .number({ error: t("validation.required") })
        .int()
        .positive({ error: t("validation.required") }),
      code: z
        .string()
        .trim()
        .min(1, { error: t("validation.required") })
        .max(codeMax, { error: t("validation.tooLong", { max: codeMax }) })
        // Checked in upper case, exactly what will be stored ("os-01" is fine).
        .refine((code) => workspaceCodePattern.test(normalizeCode(code)), {
          error: t("workspaces.validation.code"),
        }),
      type: z.enum(workspaceTypes, { error: t("validation.required") }),
      capacity: z
        .number({ error: t("validation.required") })
        .int({ error: t("validation.wholeNumber") }),
    })
    .superRefine((values, context) => {
      // A rule that needs two fields: runs only when each field is valid on its own.
      const { min, max } = capacityRange(values.type);
      if (values.capacity < min || values.capacity > max) {
        context.addIssue({
          code: "custom",
          path: ["capacity"],
          message:
            min === max
              ? t("validation.exactly", { value: min })
              : t("validation.between", { min, max }),
        });
      }
    });
}

export type WorkspaceFormValues = z.input<ReturnType<typeof workspaceSchema>>;
