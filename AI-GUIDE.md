# AI Assistant — User Guide

RetroC IDE includes an optional **local AI assistant** powered by [LLamaSharp](https://github.com/SciSharp/LLamaSharp) and a small language model (Phi-3 mini). Everything runs **100% offline on your machine** — no cloud, no API keys, no data leaves your PC.

---

## Quick Start

1. **Open the AI panel** — Press `Ctrl+I` or click **Options → AI Settings** and close the dialog.
2. **Type a question** in the input box and press `Ctrl+Enter` (or click ▶).
3. On first use the model (~2.3 GB) is **downloaded automatically**. This is a one-time download — subsequent launches load instantly.

---

## Two Modes

### Ask Mode (default)

Use Ask mode to **learn, debug, and explore** C code.

| What you can do | How |
|-----------------|-----|
| Ask a C question | Type in the chat box |
| Explain your code | Select code in the editor → right-click → **Ask AI** |
| Explain a compiler error | Right-click an error in the Message panel → **Explain Error with AI** |
| Get code examples | Ask "write a linked list in C" |

Responses include syntax-highlighted code blocks with **Copy** and **Insert** buttons.

### Agent Mode

Switch to Agent mode by clicking the **Agent** tab at the top of the AI panel.

Use Agent mode to **let the AI edit your code directly**.

| What you can do | How |
|-----------------|-----|
| Request a code change | "Add input validation to main()" |
| Create a new file | "Create a header file for my stack" |
| Fix an error | "Fix the segfault on line 12" |

The AI responds with structured edit blocks showing a **diff view** (red = removed, green = added). Each block has:

- **Apply** — Applies the edit to your file and auto-compiles.
- **Reject** — Discards the suggestion.

> **Tip:** Agent mode always sends your current file content to the AI so it can reference your code accurately.

---

## Context Attachment

The 📎 button in the chat input area controls whether the **current editor context** (file name, content, cursor position, compiler errors) is sent with your message.

- **On** (default) — The AI sees your code and can give targeted answers.
- **Off** — Only your typed message is sent (useful for general C questions).
- **Agent mode** — Context is always sent, regardless of this toggle.

---

## AI Settings

Open **Options → AI Settings** to configure:

| Setting | Default | Description |
|---------|---------|-------------|
| **Model** | Phi-3 mini Q4 | Shows download status. Download / Delete / Browse for custom `.gguf` model. |
| **GPU Layers** | 0 (CPU only) | Number of layers offloaded to GPU. Increase if you have a dedicated GPU with VRAM. |
| **Context Size** | 4096 tokens | How much conversation + code the AI can "see" at once. Larger = more memory. |
| **Max Tokens** | 2048 | Maximum length of AI responses. |
| **Temperature** | 0.3 | Controls randomness. Lower = more predictable, higher = more creative. |

### Using a Custom Model

You can use any GGUF-format model:

1. Open **Options → AI Settings**.
2. Click **Browse** and select your `.gguf` file.
3. The AI will use your model on the next message.

> **Note:** The `edit:` / `create:` format in Agent mode works best with instruction-tuned models (Phi-3, Llama 3, Mistral, etc.). Very small models (<3B) may not follow the format reliably.

---

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Ctrl+I` | Toggle AI panel |
| `Ctrl+Enter` | Send message |
| `Escape` | Cancel current AI response |

---

## System Requirements for AI

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| RAM | 4 GB free | 8 GB free |
| Disk | 2.5 GB (model) | 2.5 GB |
| CPU | Any x64 with AVX2 | Modern 4+ core |
| GPU | Not required | Any with 4+ GB VRAM (set GPU Layers > 0) |

---

## How It Works

- The AI runs entirely locally using **LLamaSharp** (C# bindings for llama.cpp).
- The default model is **Phi-3 mini 4K instruct** (Q4 quantized, ~2.3 GB) from Microsoft.
- Model files are stored in `%LOCALAPPDATA%\RetroC-IDE\models\`.
- No internet connection is needed after the initial model download.
- Your code and conversations never leave your machine.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "Model not found" after download failure | Check your internet connection and retry by sending any message. Or manually place a `.gguf` file in `%LOCALAPPDATA%\RetroC-IDE\models\`. |
| Slow responses | Reduce **Context Size** to 2048, reduce **Max Tokens**, or offload layers to GPU. |
| Gibberish / repetitive output | Lower the **Temperature** to 0.1–0.2. |
| Agent mode doesn't produce edit blocks | The model may be too small. Use a 7B+ parameter model for reliable Agent mode. |
| App crashes on AI load | Your CPU may not support AVX2. Check with `systeminfo` or try a different machine. |
| High memory usage | The Q4 model needs ~3 GB RAM. Close other apps or use a smaller quantization. |

---

## Privacy

- **No telemetry** — The AI feature collects zero data.
- **No cloud calls** — All inference is local.
- **No API keys** — Nothing to configure or pay for.
- Model is downloaded once from [HuggingFace](https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf) (Microsoft's official repo).
