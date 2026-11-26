import { Group, Pagination, Text } from "@mantine/core";
import { useTranslation } from "react-i18next";

type PaginationBarProps = {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  onChange: (page: number) => void;
};

/** "11–20 of 47" + page buttons, for any PagedResult from the API. Hidden pager on a single page. */
export function PaginationBar({
  page,
  pageSize,
  totalCount,
  totalPages,
  onChange,
}: PaginationBarProps) {
  const { t } = useTranslation();

  if (totalCount === 0) {
    return null;
  }

  const from = (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);

  return (
    <Group justify="space-between" mt="md" gap="sm">
      <Text size="sm" c="dimmed">
        {t("common.pagination", { from, to, total: totalCount })}
      </Text>
      {totalPages > 1 && (
        <Pagination value={page} total={totalPages} onChange={onChange} size="sm" />
      )}
    </Group>
  );
}
