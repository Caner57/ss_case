import type { ConfigurationType } from '../api/types';

interface ValueFieldProps {
  type: ConfigurationType;
  value: string;
  onChange: (next: string) => void;
}

/**
 * Renders the Value input adapted to the selected Type: a true/false select for bool, a numeric
 * input for int/double, and free text for string. This mirrors the API's server-side validation
 * (CFG-5.2) in the UI so invalid combinations are hard to submit in the first place.
 */
export function ValueField({ type, value, onChange }: ValueFieldProps) {
  if (type === 'bool') {
    return (
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="true">true</option>
        <option value="false">false</option>
      </select>
    );
  }

  if (type === 'int' || type === 'double') {
    return (
      <input
        type="number"
        step={type === 'double' ? 'any' : '1'}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    );
  }

  return (
    <input
      type="text"
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  );
}

/** The natural starting Value for a freshly selected type, so switching type never leaves it stale. */
export function defaultValueForType(type: ConfigurationType): string {
  return type === 'bool' ? 'true' : '';
}
