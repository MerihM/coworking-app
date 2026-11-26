// Loads data for a page and keeps it in state – the pattern from StaffListPage (CW-17) as a hook,
// so pages that load more than one thing (room + its workspaces) don't repeat it.
import { useCallback, useEffect, useState } from "react";
import type { ApiProblem } from "./problem";
import type { ApiResult } from "./result";

type Loaded<T> = { key: string; data?: T; problem?: ApiProblem };

export type ApiQueryState<T> = {
  /** The last successful answer. Kept while a new query loads, so tables don't flash empty. */
  data: T | undefined;
  /** Set only when the CURRENT query failed. */
  problem: ApiProblem | undefined;
  /** true until the answer for the current query (and reload count) has arrived. */
  loading: boolean;
  /** Loads the same query again, e.g. after something was saved. */
  reload: () => void;
};

/**
 * Calls `loader(query)` whenever `query` changes and returns the answer.
 *
 * Rules (react-hooks v7 lint):
 * - `loader` must be stable – pass a module-level function such as `listRooms`, not an inline arrow.
 * - `query` must be stable too – a primitive (an id) or an object built with `useMemo`.
 *   A new object on every render would load forever.
 *
 * State is only set inside `.then`, and an answer for an older query is ignored (the `current`
 * flag), so fast typing in a search box can never show stale rows.
 */
export function useApiQuery<Q, T>(
  loader: (query: Q) => Promise<ApiResult<T>>,
  query: Q,
): ApiQueryState<T> {
  const [reloadCount, setReloadCount] = useState(0);
  const key = `${JSON.stringify(query)}#${reloadCount}`;
  const [loaded, setLoaded] = useState<Loaded<T> | null>(null);

  useEffect(() => {
    let current = true;
    void loader(query).then((result) => {
      if (current) {
        setLoaded(result.ok ? { key, data: result.data } : { key, problem: result.problem });
      }
    });
    return () => {
      current = false;
    };
  }, [loader, query, key]);

  const reload = useCallback(() => setReloadCount((count) => count + 1), []);
  const loading = loaded?.key !== key;

  return {
    data: loaded?.data,
    problem: loading ? undefined : loaded?.problem,
    loading,
    reload,
  };
}
