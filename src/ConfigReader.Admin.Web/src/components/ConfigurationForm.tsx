import { useState } from 'react';
import { ApiError, configurationsApi } from '../api/client';
import { CONFIGURATION_TYPES, type ConfigurationType } from '../api/types';
import { ValueField, defaultValueForType } from './ValueField';

interface ConfigurationFormProps {
  onCreated: () => void;
}

interface FormState {
  name: string;
  type: ConfigurationType;
  value: string;
  isActive: boolean;
  applicationName: string;
}

const initialState: FormState = {
  name: '',
  type: 'string',
  value: defaultValueForType('string'),
  isActive: true,
  applicationName: '',
};

/**
 * Create form for a new configuration record (Id is assigned by the store, so it is absent). On
 * a successful POST it clears itself and asks the parent to reload the list; on a rejected request
 * it surfaces the API's validation message (CFG-5.2).
 */
export function ConfigurationForm({ onCreated }: ConfigurationFormProps) {
  const [form, setForm] = useState<FormState>(initialState);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const update = <K extends keyof FormState>(field: K, next: FormState[K]) =>
    setForm((current) => ({ ...current, [field]: next }));

  // Switching type resets Value to that type's natural default, so a leftover value from the
  // previous type can never be submitted (e.g. free text lingering after choosing bool).
  const changeType = (type: ConfigurationType) =>
    setForm((current) => ({ ...current, type, value: defaultValueForType(type) }));

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await configurationsApi.create(form);
      setForm(initialState);
      onCreated();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Yapılandırma oluşturulamadı.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form className="config-form" onSubmit={handleSubmit}>
      <h2>Yapılandırma Ekle</h2>
      <div className="form-grid">
        <label>
          <span>Ad</span>
          <input
            type="text"
            value={form.name}
            required
            onChange={(event) => update('name', event.target.value)}
          />
        </label>

        <label>
          <span>Tür</span>
          <select value={form.type} onChange={(event) => changeType(event.target.value as ConfigurationType)}>
            {CONFIGURATION_TYPES.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </label>

        <label>
          <span>Değer</span>
          <ValueField type={form.type} value={form.value} onChange={(next) => update('value', next)} />
        </label>

        <label>
          <span>Uygulama</span>
          <input
            type="text"
            value={form.applicationName}
            required
            onChange={(event) => update('applicationName', event.target.value)}
          />
        </label>

        <label className="checkbox">
          <input
            type="checkbox"
            checked={form.isActive}
            onChange={(event) => update('isActive', event.target.checked)}
          />
          <span>Aktif</span>
        </label>
      </div>

      {error && <p className="error">{error}</p>}

      <button type="submit" className="primary" disabled={isSubmitting}>
        {isSubmitting ? 'Kaydediliyor…' : 'Ekle'}
      </button>
    </form>
  );
}
