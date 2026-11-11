# 🎾 Pixel Tennis — Virtual Court Championship
, Intermediário, Avançado e Profissional). Cada partida é **melhor de 3 sets**, e o objetivo é vencer a final do nível 5 e se tornar o **Grande Campe
Jogo de tênis arcade em **2D pixel art**, desenvolvido na **Unity 6**, em que o jogador enfrenta uma inteligência artificial em um torneio de **5 níveis** de dificuldade crescente (Iniciante, Amadorão do Circuito**.

> Projeto acadêmico. O Game Design Document (GDD) completo acompanha este repositório/entrega.

---

## 👥 Integrantes
[Felipe Alexandre Pereira] | [Programação]
[Gustavo Felipe Bento  Lopes] | [Design e arte]
[Lucas Barroso Silvestrini] | [Áudio e testes]
[Victor Augusto Farias Ferreira] | [Programação]

**Disciplina:** [Desenvolvimento de Jogos Digitais]
**Professor(a):** [Luis Filipe Grael Tinos]
---

## 🎮 Como jogar

### Objetivo

Vença **2 sets** antes do adversário em cada partida. Um set é ganho por quem chega a **5 pontos com diferença mínima de 2**. Ao vencer, você avança de nível; ao perder em **qualquer** nível, o torneio termina e recomeça do zero.

### Controles (teclado)

| Ação | Tecla |
|---|---|
| Mover o personagem | `W` `A` `S` `D` ou setas direcionais |
| Sacar / Rebater | `Espaço` |
| Confirmar em menus | `Espaço` ou `Enter` |
| Pausar | `Esc` |
| Reiniciar (no menu de pausa) | `R` |
| Voltar ao menu principal | `M` ou `Esc` |
| Sair do jogo | `Q` |

**Dica:** movimente-se lateralmente **no momento da rebatida** para aplicar efeito (spin) e fazer a bola curvar.

---

## ⚙️ Requisitos

| Item | Requisito |
|---|---|
| Unity Editor | **6000.5.7f1** (Unity 6) |
| Módulo de build | *Windows Build Support* (para gerar o executável) |
| Sistema operacional | Windows 10 ou superior (64 bits) |
| Git | Para clonar o repositório |

> A versão exata do editor está em `ProjectSettings/ProjectVersion.txt`. Usar outra versão pode causar avisos de atualização do projeto.

---

## ▶️ Executando o jogo

### Opção 1 — Pelo Unity Editor (recomendado)

1. **Instale o Unity Hub** em <https://unity.com/download> e, por ele, instale o editor **6000.5.7f1** (use a aba *Installs* → *Install Editor* → *Archive*, se a versão não aparecer na lista).
2. **Clone o repositório:**
   ```bash
   git clone https://github.com/felipeap12/Tennis-Pixel.git
   ```
3. No Unity Hub, clique em **Add → Add project from disk** e selecione a pasta `Tennis-Pixel`.
4. Abra o projeto. A **primeira abertura demora** alguns minutos, pois a Unity importa todos os assets e gera a pasta `Library`.
5. No painel *Project*, abra a cena principal do jogo:

   ```
   Assets/Josee/Aces_in_pixel/Sample Scene/Sample Scene.unity
   ```

   > ⚠️ Atenção: a cena `Assets/Scenes/SampleScene` **não** contém o jogo. Use a cena indicada acima.
6. Clique no botão **▶ Play** no topo do editor.
7. Se o teclado não responder, **clique dentro da janela *Game*** para dar foco a ela. Para melhor visualização, use a proporção **4:3** ou *Free Aspect* e ative *Maximize On Play*.

---

## 🗂️ Estrutura do projeto

```
Tennis-Pixel/
├── Assets/
│   ├── Animation/            # Animações e controllers (Idle, Run, Hit)
│   ├── Audio/ e Resources/   # Música de fundo
│   ├── Josee/Aces_in_pixel/  # Pacote de arte e cena principal do jogo
│   ├── Scenes/               # Cena de exemplo (sem o jogo)
│   ├── Scripts/              # Código do jogo (C#)
│   ├── Settings/             # Configurações do URP
│   └── Tilemap/              # Tiles da quadra
├── Packages/                 # Dependências (manifest.json)
└── ProjectSettings/          # Configurações do projeto Unity
```

### Scripts principais (`Assets/Scripts`)

| Script | Responsabilidade |
|---|---|
| `GameManager` | Estado do jogo: pontos, sets, níveis, vitória, derrota e pausa |
| `PlayerController` | Movimento e rebatida do jogador |
| `EnemyAIController` | Comportamento e dificuldade da IA |
| `Ball` | Física da bola, saque, efeito e pontuação |
| `ScoreboardHUD` | Placar, anúncios e telas (pausa, vitória, derrota) |
| `LoginManager` | Menu inicial |
| `AudioManager` | Música e efeitos sonoros |

---

## 🛠️ Tecnologias

- Unity 6 (6000.5.7f1) com Universal Render Pipeline (URP 17.5.0)
- C#
- TextMeshPro e Unity UI (uGUI)
- 2D Tilemap

---

## 🙏 Créditos

- **Arte:** [Tennis Asset Pack - Aces in pixel](https://assetstore.unity.com/packages/2d/tennis-asset-pack-aces-in-pixel-335938), de **Josee**, adquirido por compra na Unity Asset Store (licença *Single Entity*, conforme o [EULA da Asset Store](https://unity.com/legal/as-terms)).
- **Música:** `backgroundMusic.mp3`, gerada por inteligência artificial (Google Gemini).
- **Engine:** Unity Technologies.

---

## 📄 Licença

Projeto desenvolvido para fins acadêmicos. Os assets de terceiros seguem as licenças indicadas na seção de créditos.
