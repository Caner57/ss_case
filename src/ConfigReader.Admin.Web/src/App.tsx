import { useMemo, useState } from 'react';
import type { Configuration } from './api/types';
import { ConfigurationForm } from './components/ConfigurationForm';
import { ConfigurationTable } from './components/ConfigurationTable';
import { EditConfigurationDialog } from './components/EditConfigurationDialog';
import { useConfigurations } from './hooks/useConfigurations';

export default function App() {
  const { configurations, isLoading, error, reload } = useConfigurations();
  const [nameFilter, setNameFilter] = useState('');
  const [editing, setEditing] = useState<Configuration | null>(null);

  // Client-side name filter: it narrows the already-fetched list in memory, so typing never
  // triggers a network request (a hard requirement of the case).
  const visibleConfigurations = useMemo(() => {
    const needle = nameFilter.trim().toLowerCase();
    if (!needle) {
      return configurations;
    }
    return configurations.filter((configuration) =>
      configuration.name.toLowerCase().includes(needle),
    );
  }, [configurations, nameFilter]);

  return (
    <main className="app">
      <header className="app-header">
        <h1>ConfigReader Yönetim</h1>
      </header>

      <ConfigurationForm onCreated={reload} />

      <section className="toolbar">
        <label className="filter">
          <span>İsme göre filtrele</span>
          <input
            type="search"
            value={nameFilter}
            placeholder="örn. SiteName"
            onChange={(event) => setNameFilter(event.target.value)}
          />
        </label>
      </section>

      {error && <p className="error">{error}</p>}
      {isLoading ? (
        <p className="loading">Yapılandırmalar yükleniyor…</p>
      ) : (
        <ConfigurationTable configurations={visibleConfigurations} onEdit={setEditing} />
      )}

      {editing && (
        <EditConfigurationDialog
          configuration={editing}
          onClose={() => setEditing(null)}
          onSaved={reload}
        />
      )}
    </main>
  );
}
