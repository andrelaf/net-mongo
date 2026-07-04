// Orientações didáticas por conceito. Ficam no front para dar contexto ao aluno
// além do que a API já devolve (explicação + código + comando real).

export interface ConceptGuide {
  summary: string;
  whenToUse: string[];
  watchFor: string[]; // o que observar no resultado / boas práticas
}

export const conceptGuides: Record<string, ConceptGuide> = {
  CRUD: {
    summary:
      "Operações básicas de criar, ler, atualizar e apagar. Compare o Driver (operadores atômicos explícitos como $set/$inc) com o EF Core (change tracking + SaveChanges).",
    whenToUse: [
      "Driver: quando quiser controle fino e operadores atômicos.",
      "EF Core: quando a produtividade e o LINQ familiar importam mais.",
    ],
    watchFor: [
      "No update do Driver, veja o operador $set/$inc no comando gerado.",
      "No EF, o update é inferido pelo change tracking a partir do que você alterou.",
    ],
  },
  Filtros: {
    summary:
      "Selecionar documentos (o $match / o comando find). Há três formas no Driver: Builders (tipado), LINQ e BsonDocument. Filtros bem escritos usam índices.",
    whenToUse: [
      "Builders: filtros dinâmicos e componíveis.",
      "LINQ: quando a equipe já pensa em LINQ.",
      "Array/Texto: buscas em coleções embutidas ou full-text.",
    ],
    watchFor: [
      "Observe o campo 'filter' no comando find capturado.",
      "Filtros em array usam índice multikey; $text usa índice de texto.",
      "Evite regex sem âncora e $where — não usam índice.",
    ],
  },
  "Projeção": {
    summary:
      "Escolher (e transformar) os campos retornados. Menos bytes = menos rede e menos RAM. Habilita 'covered queries'.",
    whenToUse: [
      "Sempre traga só os campos que a tela usa.",
      "$slice para prever arrays grandes (ex.: preview de reviews).",
    ],
    watchFor: [
      "Veja o campo 'projection' no comando.",
      "Campos calculados viram um estágio $project com expressões.",
    ],
  },
  "Agregação": {
    summary:
      "Pipelines executados no servidor: $match, $group, $unwind, $bucket, $lookup, $facet. É o motor analítico do MongoDB.",
    whenToUse: [
      "Relatórios, dashboards, rankings e métricas.",
      "$facet para várias métricas numa passada só.",
      "$lookup para joins — mas prefira denormalizar no caminho quente.",
    ],
    watchFor: [
      "Leia o array 'pipeline' no comando aggregate capturado.",
      "$group reduz muitos documentos a poucos — ótimo para agregados.",
      "Coloque $match/$limit cedo no pipeline para filtrar antes de agregar.",
    ],
  },
  Performance: {
    summary:
      "Índices e menos dados na rede são os maiores ganhos. Use explain() para provar se a query usa índice (IXSCAN) ou varre tudo (COLLSCAN).",
    whenToUse: [
      "explain(): sempre que uma query estiver lenta.",
      "Covered query: quando o índice cobre filtro + projeção.",
      "Keyset pagination: para paginar fundo sem Skip.",
    ],
    watchFor: [
      "totalDocsExamined baixo = bom. COLLSCAN examina a coleção inteira.",
      "Covered query examina 0 documentos (responde só do índice).",
      "Skip(N) piora conforme você pagina fundo; keyset não.",
    ],
  },
  "Modelagem": {
    summary:
      "Como estruturar os documentos. 'Dados acessados juntos ficam juntos' (embed) x relações grandes/compartilhadas (reference). Updates de 1 documento são atômicos.",
    whenToUse: [
      "Embed: relações 'contém' e de cardinalidade limitada (ex.: reviews).",
      "Reference: 1:N ilimitado ou dados compartilhados (ex.: pedidos->cliente).",
      "Denormalize campos lidos com frequência (ex.: customerName no pedido).",
    ],
    watchFor: [
      "Embedding lê tudo em 1 consulta; reference exige 2ª query ou $lookup.",
      "$push + $inc num FindOneAndUpdate é atômico — sem race condition.",
      "Snapshot: pedido congela preço/nome do produto no ato da compra.",
    ],
  },
  "EF Core": {
    summary:
      "Provider oficial MongoDB para EF Core. LINQ familiar, change tracking e owned types para documentos embutidos. Traduz LINQ para pipelines de agregação.",
    whenToUse: [
      "CRUD e consultas LINQ do dia a dia.",
      "Times que já usam EF em SQL e querem uma API única.",
    ],
    watchFor: [
      "Use AsNoTracking em leituras.",
      "Nem todo operador do Mongo tem tradução ($bucket/$facet/arrayFilters) — caia para o Driver quando faltar.",
      "Owned types (Reviews/Dimensions) carregam junto com o produto.",
    ],
  },
};
