import { describe, it, expect, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ExampleRunner } from "./ExampleRunner";
import type { ExampleInfo, ExampleResult } from "../api";

// Catálogo espelhando o backend — garante um teste por exemplo.
const catalog: ExampleInfo[] = [
  { id: "crud-driver", concept: "CRUD", title: "CRUD básico com o Driver", approach: "driver", route: "/api/crud/driver" },
  { id: "crud-ef", concept: "CRUD", title: "CRUD básico com EF Core", approach: "ef-core", route: "/api/crud/ef" },
  { id: "filter-builder", concept: "Filtros", title: "FilterDefinition (Builders)", approach: "driver", route: "/api/filters/builder" },
  { id: "filter-ef", concept: "Filtros", title: "Mesmo filtro · EF Core", approach: "ef-core", route: "/api/ef/filter" },
  { id: "filter-linq", concept: "Filtros", title: "Filtros com LINQ", approach: "driver", route: "/api/filters/linq" },
  { id: "filter-array", concept: "Filtros", title: "Filtro em array", approach: "driver", route: "/api/filters/array" },
  { id: "filter-text", concept: "Filtros", title: "Busca full-text", approach: "driver", route: "/api/filters/text" },
  { id: "projection-include", concept: "Projeção", title: "Incluir/excluir campos", approach: "driver", route: "/api/projection/include" },
  { id: "projection-computed", concept: "Projeção", title: "Campos calculados", approach: "driver", route: "/api/projection/computed" },
  { id: "projection-slice", concept: "Projeção", title: "Fatiar array", approach: "driver", route: "/api/projection/slice" },
  { id: "agg-group", concept: "Agregação", title: "Faturamento por categoria", approach: "driver", route: "/api/aggregation/revenue-by-category" },
  { id: "agg-ef", concept: "Agregação", title: "GroupBy por categoria · EF Core", approach: "ef-core", route: "/api/ef/aggregation" },
  { id: "agg-unwind", concept: "Agregação", title: "Top produtos", approach: "driver", route: "/api/aggregation/top-products" },
  { id: "agg-bucket", concept: "Agregação", title: "Faixas de preço", approach: "driver", route: "/api/aggregation/price-buckets" },
  { id: "agg-lookup", concept: "Agregação", title: "Join com $lookup", approach: "driver", route: "/api/aggregation/orders-with-customer" },
  { id: "agg-facet", concept: "Agregação", title: "Dashboard com $facet", approach: "driver", route: "/api/aggregation/dashboard" },
  { id: "perf-index", concept: "Performance", title: "COLLSCAN x IXSCAN", approach: "driver", route: "/api/performance/explain" },
  { id: "perf-covered", concept: "Performance", title: "Covered query", approach: "driver", route: "/api/performance/covered" },
  { id: "perf-pagination", concept: "Performance", title: "Paginação keyset", approach: "driver", route: "/api/performance/pagination" },
  { id: "model-embed", concept: "Modelagem", title: "Embedding vs Referencing", approach: "driver", route: "/api/modeling/embedding" },
  { id: "model-bulk", concept: "Modelagem", title: "Update atômico", approach: "driver", route: "/api/modeling/atomic-update" },
  { id: "ef-linq", concept: "EF Core", title: "Consultas LINQ", approach: "ef-core", route: "/api/ef/linq" },
  { id: "ef-projection", concept: "EF Core", title: "Projeção com Select", approach: "ef-core", route: "/api/ef/projection" },
  { id: "ef-owned", concept: "EF Core", title: "Owned types", approach: "ef-core", route: "/api/ef/owned" },
];

function fakeResult(ex: ExampleInfo): ExampleResult {
  return {
    concept: ex.concept,
    approach: ex.approach,
    explanation: `Explicação de ${ex.title}`,
    csharp: "var x = await collection.Find(_ => true).ToListAsync();",
    elapsedMs: 12,
    count: 3,
    data: [{ demo: true }],
    mongoCommands: [{ name: "find", json: '{ "find": "products" }' }],
  };
}

describe("ExampleRunner — um teste por exemplo", () => {
  it.each(catalog)("$concept · $title: renderiza, executa e mostra resultado + comando", async (ex) => {
    const user = userEvent.setup();
    const run = vi.fn().mockResolvedValue(fakeResult(ex));

    render(<ExampleRunner example={ex} run={run} />);

    // Título e orientações do conceito aparecem.
    expect(screen.getByRole("heading", { name: ex.title })).toBeInTheDocument();
    expect(screen.getByText("Quando usar")).toBeInTheDocument();
    expect(screen.getByText("O que observar")).toBeInTheDocument();

    // Executa.
    await user.click(screen.getByRole("button", { name: "Executar exemplo" }));

    // Chamou o endpoint certo com o método correto (POST para CRUD/atomic).
    const expectedMethod = ex.route.includes("/crud/") || ex.route.includes("/atomic-update") ? "POST" : "GET";
    await waitFor(() => expect(run).toHaveBeenCalledWith(ex.route, expectedMethod));

    // Métricas e explicação do resultado.
    expect(await screen.findByText(/12 ms/)).toBeInTheDocument();
    expect(screen.getByText(`Explicação de ${ex.title}`)).toBeInTheDocument();

    // Aba de comando MongoDB mostra o comando real.
    await user.click(screen.getByRole("tab", { name: "Comando MongoDB" }));
    expect(screen.getByText(/comando real enviado ao MongoDB/)).toBeInTheDocument();
  });
});

describe("ExampleRunner — comportamento", () => {
  it("mostra erro quando a execução falha", async () => {
    const user = userEvent.setup();
    const run = vi.fn().mockRejectedValue(new Error("API offline"));
    const ex = catalog[0];

    render(<ExampleRunner example={ex} run={run} />);
    await user.click(screen.getByRole("button", { name: "Executar exemplo" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("API offline");
  });

  it("alterna para a aba de código C#", async () => {
    const user = userEvent.setup();
    const ex = catalog.find((e) => e.id === "filter-builder")!;
    const run = vi.fn().mockResolvedValue(fakeResult(ex));

    render(<ExampleRunner example={ex} run={run} />);
    await user.click(screen.getByRole("button", { name: "Executar exemplo" }));
    await screen.findByText(/12 ms/);

    await user.click(screen.getByRole("tab", { name: "Código C#" }));
    expect(screen.getByText(/collection.Find/)).toBeInTheDocument();
  });

  it("rotula exemplos POST como operação de escrita", () => {
    const ex = catalog.find((e) => e.id === "crud-driver")!;
    render(<ExampleRunner example={ex} run={vi.fn()} />);
    expect(screen.getByRole("button", { name: "Executar exemplo" })).toHaveTextContent("escreve");
  });
});
