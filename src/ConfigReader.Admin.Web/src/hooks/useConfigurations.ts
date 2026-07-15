import { useCallback, useEffect, useState } from 'react';
import { ApiError, configurationsApi } from '../api/client';
import type { Configuration } from '../api/types';

interface UseConfigurationsResult {
  configurations: Configuration[];
  isLoading: boolean;
  error: string | null;
  reload: () => void;
}

/**
 * Loads the full configuration list once and exposes a reload trigger. The list is fetched a
 * single time (and on explicit reload after a write), so client-side name filtering in the view
 * never issues a network request.
 */
export function useConfigurations(): UseConfigurationsResult {
  const [configurations, setConfigurations] = useState<Configuration[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (signal: AbortSignal) => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await configurationsApi.list();
      if (!signal.aborted) {
        setConfigurations(result);
      }
    } catch (caught) {
      if (!signal.aborted) {
        setConfigurations([]);
        setError(toMessage(caught));
      }
    } finally {
      if (!signal.aborted) {
        setIsLoading(false);
      }
    }
  }, []);

  const [reloadToken, setReloadToken] = useState(0);
  const reload = useCallback(() => setReloadToken((token) => token + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load, reloadToken]);

  return { configurations, isLoading, error, reload };
}

function toMessage(caught: unknown): string {
  if (caught instanceof ApiError) {
    return caught.message;
  }
  return 'Admin API\'ye ulaşılamadı. Çalışıyor mu?';
}
