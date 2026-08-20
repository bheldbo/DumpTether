import { useCallback, useEffect, useRef, useState } from 'react';

export function useUnsavedTaskChanges(confirmMessage: string) {
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const hasUnsavedChangesRef = useRef(false);

  const updateHasUnsavedChanges = useCallback((nextValue: boolean) => {
    hasUnsavedChangesRef.current = nextValue;
    setHasUnsavedChanges(nextValue);
  }, []);

  useEffect(() => {
    if (!hasUnsavedChanges) {
      return undefined;
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [hasUnsavedChanges]);

  const confirmNavigation = useCallback(() => {
    return !hasUnsavedChangesRef.current || window.confirm(confirmMessage);
  }, [confirmMessage]);

  const hasUnsavedChangesNow = useCallback(
    () => hasUnsavedChangesRef.current,
    [],
  );

  return {
    confirmNavigation,
    hasUnsavedChanges,
    hasUnsavedChangesNow,
    setHasUnsavedChanges: updateHasUnsavedChanges,
  };
}
