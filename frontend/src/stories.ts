// Explicações em linguagem SIMPLES para as páginas de anti-padrão.
// Objetivo: qualquer pessoa (mesmo sem conhecer MongoDB) entender o caso.
// Cada história tem uma analogia do dia a dia + problema + correção.

export interface Story {
  emPalavrasSimples: string; // resumo direto, sem jargão
  analogia: string;          // comparação com algo do cotidiano
  oProblema: string;         // o que dá errado (ou o problema que a correção resolve)
  aCorrecao: string;         // o caminho certo
}

// Chaveado pelo id do exemplo (ExampleInfo.id).
export const exampleStories: Record<string, Story> = {
  "anti-unbounded": {
    emPalavrasSimples:
      "Guardar uma lista que nunca para de crescer (por exemplo, TODOS os pedidos de um cliente) dentro de um único registro.",
    analogia:
      "É como anotar todas as compras de um cliente numa única folha colada na ficha dele. No começo cabe. Com os anos, a folha vira um rolo gigante — e você precisa desenrolar tudo só para ver o nome dele.",
    oProblema:
      "Cada registro do MongoDB tem um teto de 16 MB. A lista incha até estourar esse limite. E mesmo antes disso, o banco precisa carregar a lista inteira toda vez que lê o registro, mesmo que você só queira um campinho.",
    aCorrecao:
      "Guarde os itens em sua própria 'gaveta' (coleção separada) e ligue-os por um código (referência). É o que este projeto faz: pedidos ficam na coleção 'orders' apontando para o cliente.",
  },
  "anti-subset": {
    emPalavrasSimples:
      "Como mostrar algumas avaliações de um produto sem cair na armadilha da lista infinita.",
    analogia:
      "Na vitrine da loja você mostra as 3 avaliações mais recentes. Quem quiser ler todas clica em 'ver mais' — e só aí você vai buscar a lista completa no estoque.",
    oProblema:
      "Se embutir TODAS as avaliações, você recria o array ilimitado. Se guardar tudo separado, cada tela precisa de uma busca extra só para mostrar 3 comentários.",
    aCorrecao:
      "Subset Pattern: mantenha só um punhado 'quente' (as 3 recentes) junto do produto e deixe o histórico completo numa coleção à parte, buscada apenas quando alguém pedir.",
  },
  "anti-indexes": {
    emPalavrasSimples:
      "Criar índices 'só por garantia', em campos que você nem consulta.",
    analogia:
      "Índice é como o sumário de um livro: ajuda a achar rápido. Mas cada sumário extra precisa ser reescrito toda vez que o livro muda. Ter 10 sumários que ninguém usa só faz cada edição do livro demorar mais.",
    oProblema:
      "Todo índice acelera a leitura, mas é atualizado em CADA gravação e ocupa disco e memória. Índices que ninguém usa só deixam a escrita mais lenta e o banco mais pesado — sem nenhum ganho.",
    aCorrecao:
      "Crie índice apenas para os filtros e ordenações que a aplicação realmente faz. Use '$indexStats' para descobrir e remover os que ninguém usa.",
  },
  "anti-bucket": {
    emPalavrasSimples:
      "Quando você tem muitíssimas leituras pequenas (sensores, cliques, logs), não crie um registro para cada uma.",
    analogia:
      "Em vez de um recibo separado para cada gole de café do dia, você anota tudo numa folha por dia: total, média, e a listinha dos horários. Uma folha por dia em vez de centenas de recibos soltos.",
    oProblema:
      "Um documento por leitura gera milhões de registros minúsculos. Isso enche o banco de _id's e índices e fica caro de manter e consultar.",
    aCorrecao:
      "Bucket Pattern: agrupe as leituras de uma janela de tempo (ex.: 1 hora) em UM documento, com a lista das medições e já os totais (mín/máx/média) calculados. Aqui: de 240 leituras para 24 baldes.",
  },
};
