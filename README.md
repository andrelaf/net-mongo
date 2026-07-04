# 🍃 .NET 10 + MongoDB — exemplos guiados (Driver oficial & EF Core)

Projeto didático que demonstra, na prática, como usar o **MongoDB** a partir do
**.NET 10** de duas formas complementares:

- **MongoDB.Driver 3.9** — o client oficial de baixo nível, com acesso a todo o
  poder do MongoDB (pipelines de agregação, operadores atômicos, índices, explain).
- **MongoDB.EntityFrameworkCore 10** — o provider oficial de **EF Core 10**, com
  LINQ familiar, change tracking e _owned types_ para documentos embutidos.

Cada exemplo é exposto por um endpoint da API e por uma tela no front **React**,
que mostra: a **explicação** do conceito, o **código C#**, o **comando MongoDB real**
enviado ao servidor (capturado via _command monitoring_), o **resultado** e **dicas
de boas práticas**.

> Por que mostrar o comando real? Porque é a melhor forma de _provar_ o que uma
> `FilterDefinition`, um `Select` LINQ ou um pipeline de agregação realmente geram
> no MongoDB.

---

## Arquitetura

```
net-mongo/
├── docker-compose.yml            # MongoDB 8 local
├── backend/
│   ├── src/MongoDemo.Api/        # ASP.NET Core Minimal API (.NET 10)
│   │   ├── Domain/               # Modelagem: Category, Product, Order, Customer
│   │   ├── Data/                 # MongoContext (driver), AppDbContext (EF), seeder,
│   │   │                         #   captura de comandos, índices
│   │   └── Features/             # Endpoints por conceito
│   └── tests/MongoDemo.Tests/    # xUnit + Testcontainers (MongoDB real e efêmero)
└── frontend/                     # React + Vite + TypeScript + Vitest
```

### Domínio (e-commerce) e decisões de modelagem

O domínio foi escolhido para exercitar os principais _trade-offs_ de modelagem no
MongoDB:

| Entidade | Decisão | Por quê |
|---|---|---|
| `Product.Reviews` | **Embedding** | Relação "contém", cardinalidade limitada, lida junto com o produto → 1 leitura, zero joins. |
| `Product.CategoryName` | **Denormalização** (Extended Reference) | Guarda o `CategoryId` (fonte da verdade) **e** o nome, muito lido e pouco mutável → evita `$lookup` na listagem. |
| `Product.RatingAvg/Count` | **Agregado pré-calculado** | Atualizado na escrita para não recalcular a média a cada leitura. |
| `Order.Lines` | **Snapshot embutido** | Um pedido é um fato histórico: congela nome/preço do produto no ato da compra. |
| `Order.CustomerId` | **Referencing** | Relação 1:N ilimitada (muitos pedidos) → nunca embutir pedidos no cliente. |

Regra de ouro: **dados acessados juntos ficam juntos**; relações grandes,
ilimitadas ou compartilhadas são referenciadas.

---

## Como rodar

Pré-requisitos: **.NET 10 SDK**, **Node 20+**, **Docker**.

### 1) Subir o MongoDB

```bash
docker compose up -d
```

### 2) Backend (API)

```bash
cd backend/src/MongoDemo.Api
dotnet run
```

- No startup a API cria os **índices** e **semeia** dados de exemplo
  (idempotente e determinístico).
- Swagger/OpenAPI: `GET /openapi/v1.json`. Health: `GET /health`.
- A porta é definida em `Properties/launchSettings.json` (por padrão `http://localhost:5083`).

A connection string fica em `appsettings.json` (seção `Mongo`) e pode ser
sobrescrita por variável de ambiente `Mongo__ConnectionString`.

### 3) Frontend (React)

```bash
cd frontend
npm install
npm run dev
```

Abra o endereço que o Vite imprimir. Se a API não estiver em `http://localhost:5083`,
aponte com `VITE_API_URL` (ex.: crie um `.env` com `VITE_API_URL=http://localhost:5083`).

---

## Exemplos cobertos

| Conceito | Exemplos |
|---|---|
| **CRUD** | Driver (`$set`/`$inc`, `FindOneAndUpdate`) vs EF Core (change tracking) |
| **Filtros** | `Builders` tipado · LINQ (`AsQueryable`) · array multikey (`AnyIn`/`All`) · full-text (`$text`) |
| **Projeção** | incluir/excluir campos · campos calculados (`$project`) · `$slice` em array |
| **Agregação** | `$group` por categoria · `$unwind`+`$group` (top produtos) · `$bucket` (histograma) · `$lookup` (join) · `$facet` (dashboard) |
| **Performance** | `explain()` COLLSCAN×IXSCAN · covered query (0 docs examinados) · paginação keyset vs `Skip` |
| **Modelagem** | embedding vs referencing na prática · update atômico (`$push`+`$inc`) |
| **EF Core** | LINQ · projeção com `Select` · owned types (embutidos) |

---

## Boas práticas destacadas no código

- **`MongoClient` é singleton** (gerencia o pool de conexões) — nunca crie um por request.
- **`Decimal128` para dinheiro** — nunca `double`.
- **Índices são a decisão de performance nº 1**: índice composto seguindo a regra
  **ESR** (Equality → Sort → Range), multikey para arrays, texto para full-text, únicos para SKU/e-mail.
- **Traga só o necessário** (projeção) — habilita _covered queries_.
- **Coloque `$match`/`$limit` cedo** no pipeline de agregação.
- **Prefira denormalização a `$lookup`** no caminho quente; use `$lookup` em relatórios.
- **Updates de 1 documento são atômicos** — use `$inc`/`$push` em vez de ler-modificar-gravar.
- **EF Core**: `AsNoTracking` em leitura; caia para o Driver quando faltar tradução LINQ
  (`$bucket`, `$facet`, `arrayFilters`).

---

## Testes

### Backend (integração, com MongoDB real via Testcontainers)

```bash
dotnet test backend/tests/MongoDemo.Tests
```

Sobe um container MongoDB efêmero e exercita **todos** os endpoints (um teste por
conceito), além de asserções específicas — por exemplo, que a query indexada
examina menos documentos que o COLLSCAN e que a _covered query_ examina **0**
documentos. Requer Docker rodando. (27 testes.)

### Frontend (Vitest + Testing Library)

```bash
cd frontend
npm test
```

Um teste por exemplo do catálogo: renderiza a tela, executa (com a chamada de API
_mockada_), valida método correto (GET/POST), métricas, abas e a exibição do
comando MongoDB. (25 testes.)

---

## Nota técnica: captura do comando MongoDB

A API assina o evento `CommandStartedEvent` do driver e, via um `AsyncLocal` ligado
ao escopo da requisição, associa cada comando enviado à resposta daquele endpoint.
Isso permite ao front mostrar o `find`/`aggregate`/`update` exatamente como chega
ao servidor. Sob o `TestServer` (testes) o `ExecutionContext` não é propagado, então
lá validamos apenas a forma da resposta; a captura em si é exercida rodando a API
real na Kestrel.
