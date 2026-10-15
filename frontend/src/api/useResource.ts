import { useEffect, useState } from "react";
import axios from "axios";
import { ApiError, request } from "./client";
export function useResource<T>(url: string) {
  const [state, setState] = useState<{
    url: string;
    data?: T;
    error?: ApiError;
  }>({ url: "" });
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setState({ url });
    request<T>(url, { signal: controller.signal })
      .then((data) => {
        if (!controller.signal.aborted) setState({ url, data });
      })
      .catch((error) => {
        if (!controller.signal.aborted && !axios.isCancel(error))
          setState({ url, error });
      });
    return () => controller.abort();
  }, [url, attempt]);
  return {
    data: state.url === url ? state.data : undefined,
    error: state.url === url ? state.error : undefined,
    retry: () => setAttempt((x) => x + 1),
  };
}
