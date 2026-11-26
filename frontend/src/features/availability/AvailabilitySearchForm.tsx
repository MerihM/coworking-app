// The search form on /availability (FR02). It only offers dates and times that pass BR04
// (working hours, at least 1 hour), so a visitor can't build a search the API will refuse.
import { Button, NumberInput, Paper, Select, SimpleGrid, Stack, Text } from "@mantine/core";
import { DatePickerInput } from "@mantine/dates";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { workspaceTypeLabelKeys } from "../workspaces/workspaceTypes";
import {
  hasCapacityFilter,
  isSearchType,
  MIN_CAPACITY_RANGE,
  searchTypes,
  type AvailabilitySearch,
} from "./searchParams";
import {
  addDays,
  fitTimes,
  fromOptions,
  isSelectableDate,
  MAX_DAYS_AHEAD,
  toOptions,
  type CenterNow,
} from "./workingHours";

type Props = {
  /** Values to start with (from the URL, or the default search). Read once – the page re-mounts
   * the form with a `key` when the URL changes. */
  initial: AvailabilitySearch;
  /** "Now" in Sarajevo, fixed when the page opened. */
  now: CenterNow;
  /** A search is running – the button shows a spinner. */
  loading: boolean;
  onSearch: (search: AvailabilitySearch) => void;
};

export function AvailabilitySearchForm({ initial, now, loading, onSearch }: Props) {
  const { t } = useTranslation();
  const [values, setValues] = useState(initial);

  const froms = fromOptions(values.date, now);
  const tos = toOptions(values.date, values.from);
  const closed = froms.length === 0;

  const typeOptions = searchTypes.map((type) => ({
    value: type,
    label: type === "all" ? t("availability.form.allTypes") : t(workspaceTypeLabelKeys[type]),
  }));

  // Changing the date or the start re-fits the other times in the SAME update (no effect), so the
  // selects never show an impossible combination such as Saturday 19:00.
  function changeDate(date: string | null) {
    if (date) {
      setValues((current) => ({
        ...current,
        date,
        ...fitTimes(date, current.from, current.to, now),
      }));
    }
  }

  function changeFrom(from: string | null) {
    if (from) {
      setValues((current) => ({
        ...current,
        ...fitTimes(current.date, from, current.to, now),
      }));
    }
  }

  function changeTo(to: string | null) {
    if (to) {
      setValues((current) => ({ ...current, to }));
    }
  }

  function changeType(type: string | null) {
    if (isSearchType(type)) {
      setValues((current) => ({
        ...current,
        type,
        minCapacity: hasCapacityFilter(type) ? current.minCapacity : null,
      }));
    }
  }

  function changeMinCapacity(value: number | string) {
    // NumberInput gives "" when the field is emptied → any size.
    setValues((current) => ({
      ...current,
      minCapacity: typeof value === "number" ? value : null,
    }));
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onSearch(values);
  }

  return (
    <Paper component="form" onSubmit={handleSubmit} withBorder p="md" radius="md">
      <Stack gap="sm">
        <SimpleGrid cols={{ base: 1, xs: 3 }}>
          <DatePickerInput
            label={t("availability.form.date")}
            value={values.date}
            onChange={changeDate}
            minDate={now.date}
            maxDate={addDays(now.date, MAX_DAYS_AHEAD)}
            // Sundays, and today once the last start time has passed.
            excludeDate={(date) => !isSelectableDate(date, now)}
            valueFormat="ddd, D MMM YYYY"
          />
          <Select
            label={t("availability.form.from")}
            data={froms}
            value={values.from}
            onChange={changeFrom}
            allowDeselect={false}
            disabled={closed}
            maxDropdownHeight={240}
          />
          <Select
            label={t("availability.form.to")}
            data={tos}
            value={values.to}
            onChange={changeTo}
            allowDeselect={false}
            disabled={closed}
            maxDropdownHeight={240}
          />
        </SimpleGrid>

        <SimpleGrid cols={{ base: 1, xs: 3 }}>
          <Select
            label={t("availability.form.type")}
            data={typeOptions}
            value={values.type}
            onChange={changeType}
            allowDeselect={false}
          />
          {/* Desks are for one person, so the size filter only appears for offices and meeting rooms. */}
          {hasCapacityFilter(values.type) ? (
            <NumberInput
              label={t("availability.form.minCapacity")}
              placeholder={t("availability.form.minCapacityPlaceholder")}
              value={values.minCapacity ?? ""}
              onChange={changeMinCapacity}
              min={MIN_CAPACITY_RANGE.min}
              max={MIN_CAPACITY_RANGE.max}
              allowDecimal={false}
              allowNegative={false}
              clampBehavior="strict"
            />
          ) : (
            <div />
          )}
          <Button type="submit" loading={loading} disabled={closed} style={{ alignSelf: "end" }}>
            {t("availability.form.search")}
          </Button>
        </SimpleGrid>

        <Text size="xs" c={closed ? "red" : "dimmed"}>
          {closed ? t("availability.form.closed") : t("availability.hours")}
        </Text>
      </Stack>
    </Paper>
  );
}
