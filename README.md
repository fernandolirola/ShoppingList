# zezinho's Shopping List

Buys what your hideout upgrades still need, in one click.

- **Upgrade items**: every missing item that doesn't have to be found in raid is bought from traders first, then from the flea market, cheapest offer first.
- **Found-in-raid items**: if you have an unlocked craft for one and the station is built, the list buys that craft's ingredients instead. You start the craft yourself.
- **Review before buying**: a confirm screen shows every item with its icon, what it is for (donation or craft), where it comes from and its price.
- **Live buying status**: each item shows when it is bought or why it failed (sold out, price went up, no money, no space). Items that can still be bought get a "Try again" button with the new cost.
- **Plan how far ahead**: each area has a level track. Click a level to buy for every upgrade up to it, or click the next level again to skip the area. Locked levels say what they still need.
- **Pick what to buy**: tick or untick single crafts or single items. Areas and crafts can be folded.
- **Craft check**: each craft says whether your stash already covers it ("can craft x3", "can craft 1 of 2") or what is missing.
- **In raid**: press ] with the inventory open to see what you still need. Items you carry count as "on you" and the list updates as you loot. Buying stays in the hideout.
- **Sort**: sort areas by progress % or by the rarity of what is missing.
- Opens from a cart button in the hideout's left toolbar, a "Shopping list" button in each area's window, or the ] key.
- English and Portuguese (EN / PT switch in the window).

It reads your own setup: items you already own, donations kept by HideoutInProgress, the live hideout from HideoutBackport, and SVM settings that make purchases found in raid (then found-in-raid items are bought directly). Trader loyalty levels, stock, buy limits and the flea market level are respected. Purchases go through the normal trade requests, so your stash updates right away.

## Settings

Press F12 (BepInEx Configuration Manager):

| Setting | Default |
|---|---|
| Portuguese | off |
| Buy craft ingredients | on |
| Open key | ] |
| Sort by | Default |
| Retry price limit % | 10 |
| Hide ready items | off |

## Install

Extract the zip into your game folder. It adds `SPT_Runtime/user/mods/ShoppingList` (server) and `BepInEx/plugins/ShoppingList` (client). Both parts are needed.

![Install Gif](https://i.imgur.com/3N6gTe2.gif)

Not tested with Fika.

## Português

Compra o que falta para as melhorias do seu hideout, com um clique.

- **Itens de melhoria**: tudo que falta e não precisa ser encontrado em raid é comprado nos comerciantes primeiro, depois no mercado, sempre a oferta mais barata.
- **Itens FIR**: se você tem o craft desbloqueado e a estação construída, a lista compra os ingredientes do craft. Você inicia o craft.
- **Revisão antes de comprar**: uma tela de confirmação mostra cada item com ícone, para que serve (doação ou craft), de onde vem e o preço.
- **Status da compra ao vivo**: cada item mostra quando foi comprado ou por que falhou (esgotado, preço subiu, sem dinheiro, sem espaço). Itens que ainda dá para comprar ganham um botão "Tentar de novo" com o novo custo.
- **Escolha o que comprar**: marque ou desmarque áreas inteiras, crafts ou itens. Áreas e crafts podem ser recolhidos.
- **Verificação de craft**: cada craft mostra se o seu estoque já cobre ("pode craftar x3", "pode craftar 1 de 2") ou o que falta.
- **Na raid**: aperte ] com o inventário aberto para ver o que ainda falta. Itens com você contam como "com você" e a lista atualiza enquanto você saqueia. As compras ficam no hideout.
- **Planeje até onde quiser**: cada área tem uma trilha de níveis. Clique num nível para comprar para todas as melhorias até ele, ou clique de novo no próximo nível para ignorar a área. Níveis bloqueados mostram o que ainda falta.
- **Ordenar**: ordene as áreas por progresso % ou pela raridade do que falta.
- Abre por um botão de carrinho na barra esquerda do hideout, por um botão "Lista de compras" na janela de cada área, ou a tecla ].
- Inglês e português (botão EN / PT na janela).

Instale extraindo o zip na pasta do jogo. Precisa das duas partes (servidor e cliente).

## License

MIT

<sub>Enjoying the mod? Curtiu o mod? [Buy me a coffee on Ko-fi](https://ko-fi.com/zezinhoeft)</sub>
