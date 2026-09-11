# PROJECT_STATE.md — Mold System

> Fonte de verdade deste projeto. Não presuma nada que não esteja aqui.

- **Pasta:** `/Mold_System` · repo `CarlosKeese/Mold_System` (privado)
- **Tipo de tarefa:** misto
- **Status:** `NÚCLEO PRONTO — migração do acervo executada; falta a interface gráfica`
- **Acervo:** `C:\Users\CarlosKeese\Kenatec\KENATEC - Documentos\001 - Orçamentos e Projetos`

---

## O problema que ele resolve

O Carlos se perdia ao procurar um projeto. A causa era medível: as 44 pastas do
acervo misturavam **quatro** convenções no mesmo nível.

| Convenção | Exemplo |
|---|---|
| número + cliente + descrição + status | `00048 - Izan - Molde Balde 3,2 L (Pedido)` |
| número + cliente | `00003 - Precision` |
| só cliente | `Precision`, `Zagonel`, `ELO CORREIAS` |
| **invertida** — cliente no FIM | `00059 - Régua - Marcos` |
| data no lugar do número | `230701 - MOLDE NVPRO` |
| despejo | `Pré-Orçamentos`, `00000 - Sem Numereção` |

---

## A estrutura

```text
001 - Orçamentos e Projetos/
└── CLI-0015 - Precision/
    ├── 00 - Cadastro/
    ├── 01 - Orçamentos/
    │   └── ORC-00048 - Molde Balde/      só o documento e suas revisões
    ├── 02 - Produtos/
    │   └── PRD-0031 - Balde 3,2 L/       do CLIENTE, não do projeto
    └── 03 - Projetos/
        └── PRJ-0021 - Linha de Baldes/
            ├── 01 - Recebido do cliente/
            ├── 02 - Moldes/
            │   └── MLD-0019 - Molde Balde/
            │       ├── 01 - Projeto 3D/   02 - Desenhos/   03 - Eletrodos/
            │       └── 04 - CAM/          05 - Compras/    06 - Try-out e ajustes/
            ├── 03 - Dispositivos/
            └── 04 - Documentos/
```

### As decisões, e por quê

1. **Orçamento não é pasta de trabalho.** Um molde orçado cinco vezes tem cinco
   pastas `ORC-` e **um** `MLD-`. Se o trabalho morasse sob o orçamento,
   existiriam cinco cópias do mesmo molde e ninguém saberia qual é a boa.
2. **Produto pende do cliente.** É o que permite o mesmo produto entrar em
   vários projetos sem cópia — e cópia de modelo 3D é a origem clássica de
   usinar a revisão errada.
3. **Código estável + nome legível.** `MLD-0019` é a chave e nunca muda; o texto
   depois do hífen pode ser corrigido à vontade.
4. **O grafo mora no banco.** Sistema de arquivos é árvore, os dados são rede.
   Cada coisa tem UM lugar canônico; as ligações ficam no LiteDB. **Sem atalho
   nem junção** — a pasta é sincronizada por OneDrive/SharePoint, onde link
   simbólico dá problema.
5. **A camada de projeto existe sempre**, mesmo para um molde só: caminho
   previsível vale mais que uma pasta a menos, e o segundo molde não exige mover
   nada.
6. **Status fica fora do nome da pasta.** Renomear dentro do OneDrive
   re-sincroniza tudo que está dentro e invalida caminho salvo — links do Solid
   Edge inclusive, que é o tipo de quebra que só aparece semanas depois.

---

## O que existe

| Peça | Estado |
|---|---|
| `Codigos` — código, nome de pasta, sanitização | ✅ |
| `EstruturaDePastas` — taxonomia e criação | ✅ |
| `Catalogo` — LiteDB; cadastro **cria a pasta junto** | ✅ |
| `Migrador` — lê os nomes antigos e infere | ✅ com 16 testes |
| `ExecutorDaMigracao` — move, com log de desfazer | ✅ executado |
| `MoldSystem.Cli` — `sondar`, `simular`, `migrar`, `desfazer` | ✅ |
| Interface gráfica | ❌ **não existe** |

`Codigos.MaximoDoNome = 40` não é estética: o caminho base gasta ~76 caracteres
e a árvore mais funda outros ~115. O Windows corta em 260 quando
`LongPathsEnabled` está desligado — que é o caso desta máquina. `EstruturaDePastas`
recusa criar árvore que já nasceria perto do limite, **com o motivo escrito**,
porque a falha natural aparece depois, no Solid Edge, como erro de permissão.

---

## A migração, como foi

Executada em 2026-09-10 no acervo real:

| | |
|---|---|
| Pastas lidas | 44 |
| Clientes criados | 20 |
| Projetos criados | 38 |
| Movimentos | 44 |
| Para `_A classificar` | 6 |
| Falhas | 0 |

O conteúdo antigo de cada pasta foi para `01 - Recebido do cliente` do projeto
correspondente — não para a raiz, para não misturar com as pastas que a estrutura
nova cria.

**Log de desfazer:** `_migracao-desfazer.json`, na raiz do acervo. Reverter tudo:
`MoldSystem.Cli desfazer`. Ele foi gravado **antes** de cada movimento, não
depois: se a energia cair no meio, o que já saiu do lugar está registrado.

### Os 6 que ficaram na quarentena

`00000 - Sem Numereção` · `00062 - Molde Placa - Nelson` · `00067 - Moldes Frascos`
· `00068 - Molde Prodwin` · `230701 - MOLDE NVPRO` · `Pré-Orçamentos`

Em quatro deles o cliente até pode estar no nome (Nelson, Prodwin, NVPRO), mas
nenhum aparece como cliente em outra pasta — e chutar seria pior. Pasta na
quarentena é um minuto de trabalho; pasta no cliente errado é uma hora de
procura daqui a seis meses.

---

## A lição do migrador

A convenção invertida (cliente no fim) **só apareceu rodando contra o acervo
real**. A primeira versão mandava 9 pastas para a quarentena; a segunda passada
— usar o conjunto de clientes já conhecidos para achar o cliente no fim do nome
— derrubou para 6, e devolveu Izan e Marcos aos seus projetos.

Nenhum teste escrito de cabeça teria encontrado isso. Por isso os 16 casos de
teste são **nomes reais**, copiados do disco.

---

## Próxima ação

**A interface gráfica.** Hoje só existe a linha de comando, e ela resolve a
migração, que era operação de uma vez só. O uso do dia a dia — cadastrar cliente,
projeto, molde e ver a pasta nascer — precisa de janela.

Padrão da casa: .NET 10 + WPF, MVVM sem code-behind, `Result<T>`. A `Catalogo` já
expõe tudo de que a ViewModel precisa, incluindo as consultas que só o banco
responde: `ProjetosQueUsam(produtoId)` e `OrcamentosDoItem(itemId)`.

## Depois disso

- Classificar à mão as 6 pastas da quarentena.
- Ligar os orçamentos antigos: hoje a migração criou projetos, mas os PDF de
  orçamento continuam dentro de `01 - Recebido do cliente` em vez de
  `01 - Orçamentos`. Precisa de passo que reconheça `00048.pdf` e mova.
- Botão para abrir a pasta de qualquer item no Explorer.
- Busca por número de orçamento antigo (`NumeroLegado`), que é como o cliente
  cobra.
