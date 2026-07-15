import { useState } from 'react';
import { ApiError, configurationsApi } from '../api/client';
import type { Configuration } from '../api/types';
import { ValueField } from './ValueField';

interface EditConfigurationDialogProps {
  configuration: Configuration;
  onClose: () => void;
  onSaved: () => void;
}

/**
 * Edits an existing record's Value and IsActive (Name/Type/ApplicationName are shown read-only and
 * sent back unchanged, since the update endpoint validates the whole record). Saving PUTs to the
 * Admin API and asks the parent to reload; the change then reaches consuming ConfigurationReader
 * instances on their next refresh, or immediately when the Redis broker is active (CFG-3.4/4.3).
 */
export function EditConfigurationDialog({
  configuration,
  onClose,
  onSaved,
}: EditConfigurationDialogProps) {
  const [value, setValue] = useState(configuration.value);
  const [isActive, setIsActive] = useState(configuration.isActive);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);
    setIsSaving(true);
    try {
      await configurationsApi.update(configuration.id, {
        name: configuration.name,
        type: configuration.type,
        applicationName: configuration.applicationName,
        value,
        isActive,
      });
      onSaved();
      onClose();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Yapılandırma güncellenemedi.');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-modal="true" onClick={onClose}>
      <form className="dialog" onClick={(event) => event.stopPropagation()} onSubmit={handleSubmit}>
        <h2>Düzenle: {configuration.name}</h2>
        <dl className="dialog-meta">
          <div>
            <dt>Tür</dt>
            <dd>{configuration.type}</dd>
          </div>
          <div>
            <dt>Uygulama</dt>
            <dd>{configuration.applicationName}</dd>
          </div>
        </dl>

        <label className="dialog-field">
          <span>Değer</span>
          <ValueField type={configuration.type} value={value} onChange={setValue} />
        </label>

        <label className="checkbox">
          <input type="checkbox" checked={isActive} onChange={(event) => setIsActive(event.target.checked)} />
          <span>Aktif</span>
        </label>

        {error && <p className="error">{error}</p>}

        <div className="dialog-actions">
          <button type="button" onClick={onClose}>
            Kapat
          </button>
          <button type="submit" className="primary" disabled={isSaving}>
            {isSaving ? 'Kaydediliyor…' : 'Kaydet'}
          </button>
        </div>
      </form>
    </div>
  );
}
