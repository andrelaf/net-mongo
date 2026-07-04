import { useEffect, useMemo, useState } from "react";
import type { ExampleInfo } from "./api";
import { API_BASE, fetchExamples, fetchStats } from "./api";
import { ExampleRunner } from "./components/ExampleRunner";
import "./App.css";

function App() {
  const [examples, setExamples] = useState<ExampleInfo[]>([]);
  const [selected, setSelected] = useState<ExampleInfo | null>(null);
  const [stats, setStats] = useState<Record<string, number> | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    fetchExamples()
      .then((list) => {
        setExamples(list);
        setSelected(list[0] ?? null);
      })
      .catch((e) => setLoadError(e instanceof Error ? e.message : String(e)));
    fetchStats().then(setStats).catch(() => {});
  }, []);

  // Agrupa os exemplos por conceito, preservando a ordem de chegada.
  const grouped = useMemo(() => {
    const map = new Map<string, ExampleInfo[]>();
    for (const ex of examples) {
      const arr = map.get(ex.concept) ?? [];
      arr.push(ex);
      map.set(ex.concept, arr);
    }
    return [...map.entries()];
  }, [examples]);

  async function reseed() {
    await fetch(`${API_BASE}/api/reseed`, { method: "POST" });
    setStats(await fetchStats());
  }

  return (
    <div className="layout">
      <aside className="sidebar">
        <h1 className="brand">🍃 .NET + MongoDB</h1>
        <p className="tagline">Driver oficial &amp; EF Core — exemplos guiados</p>

        {stats && (
          <div className="stats">
            <span>{stats.products} produtos</span>
            <span>{stats.orders} pedidos</span>
            <span>{stats.customers} clientes</span>
          </div>
        )}
        <button className="reseed" onClick={reseed}>↻ Recriar dados</button>

        {loadError && <div className="error">API offline? {loadError}</div>}

        <nav>
          {grouped.map(([concept, items]) => (
            <div key={concept} className="nav-group">
              <h3>{concept}</h3>
              <ul>
                {items.map((ex) => (
                  <li key={ex.id}>
                    <button
                      className={selected?.id === ex.id ? "nav-item active" : "nav-item"}
                      onClick={() => setSelected(ex)}
                    >
                      {ex.title}
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </nav>
      </aside>

      <main className="content">
        {selected ? (
          <ExampleRunner key={selected.id} example={selected} />
        ) : (
          <div className="empty">
            <h2>Bem-vindo 👋</h2>
            <p>
              Suba o backend (<code>dotnet run</code>) e o MongoDB (<code>docker compose up</code>),
              escolha um exemplo à esquerda e clique em <strong>Executar</strong>.
            </p>
            <p className="muted">
              Cada exemplo mostra a explicação, o código C#, o comando MongoDB real enviado ao
              servidor e o resultado — com dicas de boas práticas.
            </p>
          </div>
        )}
      </main>
    </div>
  );
}

export default App;
