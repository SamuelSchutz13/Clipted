<p align="center">
  <img src="clipted.ico" alt="" width="72" />
</p>

<h1 align="center">Clipted</h1>

<p align="center">
  Histórico de cópias para Windows: tudo o que você copia (texto, imagens e arquivos) fica guardado num painel na bandeja do sistema. Um único <code>.exe</code>, sem instalador, sem internet e sem dependências.
</p>

<p align="center">
  <img alt="C#" src="https://img.shields.io/badge/C%23-WinForms-512bd4?logo=dotnet&logoColor=white" />
  <img alt=".NET Framework" src="https://img.shields.io/badge/.NET%20Framework-4.x-512bd4?logo=dotnet&logoColor=white" />
  <img alt="Windows" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078d4?logo=windows&logoColor=white" />
  <img alt="Sem instalador" src="https://img.shields.io/badge/instalador-nenhum-34d399" />
  <img alt="Offline" src="https://img.shields.io/badge/rede-nenhuma-34d399" />
</p>

<p align="center">
  <a href="https://github.com/SamuelSchutz13/Clipted/releases/latest"><b>Baixar a última versão</b></a>
</p>

> [!NOTE]
> **Nada sai do seu PC.** O Clipted não faz nenhuma requisição de rede. O histórico fica em `%APPDATA%\Clipted`, em arquivos de texto simples que você pode abrir, copiar ou apagar.

## Funcionalidades

- **Texto, imagens e arquivos**: tudo o que passa pelo `Ctrl+C` entra no histórico, mais recente primeiro. Copiar de novo algo repetido só o traz para o topo.
- **Pesquisa**: comece a digitar com o painel aberto e a lista filtra na hora.
- **Clique para reutilizar**: o item volta para a área de transferência e o painel se fecha, pronto para o `Ctrl+V`.
- **Shift + clique**: abre links no navegador e mostra imagens em tamanho maior.
- **Teclado** em tudo: setas para navegar, `Enter` para usar, `Delete` para apagar, `Esc` para fechar.
- **Configurações** (⚙): limite de itens, apagar itens antigos depois de um tempo, tema **claro ou escuro** (segue o Windows na primeira vez), **iniciar com o Windows**, abrir a pasta dos dados e limpar tudo.
- Respeita apps que pedem para não ser registrados (gerenciadores de senha marcam a cópia com `ExcludeClipboardContentFromMonitorProcessing`).
- Uma instância só, ícone na bandeja e suporte a telas com DPI alto.

## Como usar

1. Baixe o `Clipted.exe` na [página de releases](https://github.com/SamuelSchutz13/Clipted/releases/latest) e abra.
2. Clique no ícone da bandeja para abrir ou fechar o painel. Botão direito: **Abrir** ou **Sair**.

> [!TIP]
> O Windows SmartScreen pode avisar na primeira execução, porque o `.exe` não é assinado. Clique em **Mais informações → Executar assim mesmo**.

## Compilando

Não precisa instalar nada: o compilador C# já vem com o Windows, junto do .NET Framework 4.

```cmd
git clone https://github.com/SamuelSchutz13/Clipted.git
cd Clipted
build.cmd
```

Isso gera o `Clipted.exe` na própria pasta. Para rodar os testes:

```cmd
Clipted.exe --selftest
```

## Como funciona

```
Ctrl+C em qualquer app  →  WM_CLIPBOARDUPDATE  →  Store  →  %APPDATA%\Clipted
ícone da bandeja        →  Popup (lista + pesquisa)  →  clique  →  área de transferência
```

- **Captura**: o painel se registra como ouvinte da área de transferência do Windows, então não há polling. O que o próprio Clipted copia é ignorado.
- **Armazenamento**: `history.txt` guarda uma linha por item (data, tipo e conteúdo em Base64); imagens viram PNG em `images/`, com o hash SHA-1 como nome, então a mesma imagem nunca é salva duas vezes. Imagens fora do histórico são apagadas ao salvar.
- **Limpeza**: os itens além do limite ou mais velhos que o prazo escolhido saem a cada minuto.

### Estrutura

```
Program.cs          entrada: instância única, ícone na bandeja, timer
Popup.cs            painel: lista, pesquisa, captura da área de transferência
PopupDraw.cs        desenho dos itens da lista
PopupPreview.cs     links e pré-visualização de imagens (Shift + clique)
PopupSettings.cs    configurações e "iniciar com o Windows"
Controls.cs         botões, dropdowns e toggles desenhados à mão
Store.cs            histórico e configurações em disco
SelfTest.cs         testes (--selftest)
build.cmd           compila com o csc.exe do Windows
make-icon.ps1       gera o clipted.ico
```

## Limitações conhecidas

- Só Windows, e a interface está apenas em português.
- Não há atalho global de teclado ainda: o painel abre pelo ícone da bandeja.
- O histórico é salvo sem criptografia. Se você copia senhas de um app que não se marca como excluído, elas ficam em `history.txt` até saírem do limite ou você limpar.
