// Cliente HTTP mínimo para a API de exemplos MongoDB.
// A base pode ser sobrescrita por VITE_API_URL (útil em Docker/produção).
export const API_BASE = import.meta.env.VITE_API_URL ?? "http://localhost:5083";

export interface MongoCommand {
  name: string;
  json: string;
}

export interface ExampleResult {
  concept: string;
  approach: "driver" | "ef-core";
  explanation: string;
  csharp: string;
  elapsedMs: number;
  count: number;
  data: unknown;
  mongoCommands: MongoCommand[];
}

export interface ExampleInfo {
  id: string;
  concept: string;
  title: string;
  approach: "driver" | "ef-core";
  route: string;
}

async function handle<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`HTTP ${res.status}: ${text.slice(0, 300)}`);
  }
  return (await res.json()) as T;
}

export async function fetchExamples(): Promise<ExampleInfo[]> {
  return handle(await fetch(`${API_BASE}/api/examples`));
}

// Alguns exemplos (CRUD, update atômico) são POST porque modificam dados.
export async function runExample(route: string, method: "GET" | "POST" = "GET"): Promise<ExampleResult> {
  return handle(await fetch(`${API_BASE}${route}`, { method }));
}

export async function fetchStats(): Promise<Record<string, number>> {
  return handle(await fetch(`${API_BASE}/api/stats`));
}

// POST => operações que escrevem no banco.
export function methodFor(route: string): "GET" | "POST" {
  return route.includes("/crud/") || route.includes("/atomic-update") ? "POST" : "GET";
}
