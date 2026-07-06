import { useState } from "react";
import type { ExampleInfo, ExampleResult } from "../api";
import { methodFor, runExample as defaultRun } from "../api";
import { conceptGuides } from "../guidance";
import { exampleStories } from "../stories";
import { CodeBlock } from "./CodeBlock";

interface Props {
  example: ExampleInfo;
  // Injetável para testes (mock).
  run?: (route: string, method: "GET" | "POST") => Promise<ExampleResult>;
}

type Tab = "resultado" | "csharp" | "mongo";

export function ExampleRunner({ example, run = defaultRun }: Props) {
  const [result, setResult] = useState<ExampleResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>("resultado");

  const guide = conceptGuides[example.concept];
  const story = exampleStories[example.id];
  const method = methodFor(example.route);

  async function handleRun() {
    setLoading(true);
    setError(null);
    try {
      const r = await run(example.route, method);
      setResult(r);
      setTab("resultado");
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="runner">
      <header className="runner-head">
        <div>
          <span className={`badge badge-${example.approach}`}>
            {example.approach === "driver" ? "Driver" : "EF Core"}
          </span>
          <h2>{example.title}</h2>
          <p className="muted">
            {example.concept} · <code>{method} {example.route}</code>
          </p>
        </div>
        <button className="run-btn" onClick={handleRun} disabled={loading} aria-label="Executar exemplo">
          {loading ? "Executando…" : method === "POST" ? "Executar (escreve)" : "Executar"}
        </button>
      </header>

      {story && (
        <div className="story" data-testid="story">
          <p className="story-lead">🧠 <strong>Em palavras simples:</strong> {story.emPalavrasSimples}</p>
          <div className="story-grid">
            <div className="story-card story-analogy">
              <h4>🔎 Analogia</h4>
              <p>{story.analogia}</p>
            </div>
            <div className="story-card story-problem">
              <h4>❌ O problema</h4>
              <p>{story.oProblema}</p>
            </div>
            <div className="story-card story-fix">
              <h4>✅ A correção</h4>
              <p>{story.aCorrecao}</p>
            </div>
          </div>
        </div>
      )}

      {guide && (
        <div className="guide">
          <p>{guide.summary}</p>
          <div className="guide-cols">
            <div>
              <h4>Quando usar</h4>
              <ul>{guide.whenToUse.map((t) => <li key={t}>{t}</li>)}</ul>
            </div>
            <div>
              <h4>O que observar</h4>
              <ul>{guide.watchFor.map((t) => <li key={t}>{t}</li>)}</ul>
            </div>
          </div>
        </div>
      )}

      {error && <div className="error" role="alert">Erro: {error}</div>}

      {result && (
        <div className="result">
          <div className="metrics">
            <span className="metric">⏱ {result.elapsedMs} ms</span>
            <span className="metric">🔢 {result.count} itens</span>
            <span className="metric">📡 {result.mongoCommands.length} comando(s)</span>
          </div>

          <p className="explanation">{result.explanation}</p>

          <nav className="tabs" role="tablist">
            <button role="tab" aria-selected={tab === "resultado"} className={tab === "resultado" ? "active" : ""} onClick={() => setTab("resultado")}>Resultado</button>
            <button role="tab" aria-selected={tab === "csharp"} className={tab === "csharp" ? "active" : ""} onClick={() => setTab("csharp")}>Código C#</button>
            <button role="tab" aria-selected={tab === "mongo"} className={tab === "mongo" ? "active" : ""} onClick={() => setTab("mongo")}>Comando MongoDB</button>
          </nav>

          {tab === "resultado" && (
            <CodeBlock code={JSON.stringify(result.data, null, 2)} label="data" />
          )}
          {tab === "csharp" && <CodeBlock code={result.csharp} label="C#" />}
          {tab === "mongo" && (
            result.mongoCommands.length > 0 ? (
              result.mongoCommands.map((c, i) => (
                <CodeBlock key={i} code={c.json} label={`${c.name} (comando real enviado ao MongoDB)`} />
              ))
            ) : (
              <p className="muted">Nenhum comando capturado.</p>
            )
          )}
        </div>
      )}
    </section>
  );
}
