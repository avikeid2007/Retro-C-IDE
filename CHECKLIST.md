# Turbo C 3.0 IDE — Development Checklist

Track implementation progress for all pending features and fixes.

**Last Updated:** April 8, 2026  
**Project Status:** Beta (Core features ✅, Stubs pending ⏳)

---

## 🔴 HIGH PRIORITY (Critical / Blocks Usage)

### Core Infrastructure
- [x] Resource leaks fixed (timers disposed on unload)
- [x] ActiveEditor safe fallback (no crash on empty)
- [x] Box-drawing borders responsive (Grid-based stretching)
- [x] Block cursor dynamic sizing (scales with font)
- [x] Multi-tab editor (TabView implementation)
- [x] Add + button for new tabs
- [x] Vertical/horizontal stretching (full area usage)

**Status:** ✅ **COMPLETE**

---

## 🟠 MEDIUM PRIORITY (Feature Completeness)

### Debug Menu (4 items)
| Item | Handler | Dialog | Status |
|------|---------|--------|--------|
| Evaluate/Modify (Ctrl+F4) | ❌ | ❌ | ⏳ |
| Watches | ❌ | ❌ | ⏳ |
| Toggle Breakpoint (Ctrl+F8) | ❌ | ❌ | ⏳ |
| Breakpoints | ❌ | ❌ | ⏳ |

**Estimated effort:** 4-6 hours  
**Blocked by:** None  
**Priority:** High (basic stubs needed for menu completeness)

---

### Project Menu (6 items)
| Item | Handler | Dialog | Status |
|------|---------|--------|--------|
| Open Project | ❌ | ❌ | ⏳ |
| Close Project | ❌ | ❌ | ⏳ |
| Add Item | ❌ | ❌ | ⏳ |
| Delete Item | ❌ | ❌ | ⏳ |
| Local Options | ❌ | ❌ | ⏳ |
| Include Files | ❌ | ❌ | ⏳ |

**Estimated effort:** 8-10 hours  
**Blocked by:** Project file format design (`.PRJ`)  
**Priority:** Medium (multi-file support)

---

### Options Menu (3 items incomplete)
| Item | Handler | Dialog | Status |
|------|---------|--------|--------|
| Compiler | ✅ | ✅ | ✅ |
| Directories | ✅ | ✅ | ✅ |
| Linker | ❌ | ❌ | ⏳ |
| Make | ❌ | ❌ | ⏳ |
| Arguments | ⚠️ | ⚠️ | ⚠️ (Run-only) |
| Environment | ❌ | ❌ | ⏳ |
| Save Options | ❌ | ❌ | ⏳ |
| Retrieve Options | ❌ | ❌ | ⏳ |

**Estimated effort:** 4-5 hours  
**Blocked by:** None  
**Priority:** Medium

---

### Run Menu (2 items missing)
| Item | Handler | Status |
|------|---------|--------|
| Run Program | ✅ | ✅ |
| User Screen (Alt+F5) | ✅ | ✅ |
| Program Reset (Ctrl+F2) | ❌ | ⏳ |
| Go to Cursor (F4) | ❌ | ⏳ (needs debugger) |
| Trace Into (F7) | ❌ | ⏳ (needs debugger) |
| Step Over (F8) | ❌ | ⏳ (needs debugger) |

**Estimated effort:** 2-3 hours (reset), 8+ (trace/step)  
**Blocked by:** GDB integration for F7/F8  
**Priority:** High (reset), Low (trace/step)

---

### Syntax Highlighting (8+ functions missing)

#### String Functions (6)
- [x] `strlen()`
- [x] `strcmp()`
- [x] `strcpy()`
- [x] `strcat()`
- [x] `strchr()`
- [x] `strstr()`

#### Math Functions (7)
- [x] `sin()`
- [x] `cos()`
- [x] `tan()`
- [x] `sqrt()`
- [x] `pow()`
- [x] `ceil()`
- [x] `floor()`

#### Character/Type Functions (6)
- [x] `isalpha()`
- [x] `isdigit()`
- [x] `isspace()`
- [x] `toupper()`
- [x] `tolower()`
- [x] `atoi()` / `atof()`

#### I/O Functions (6)
- [x] `fopen()`
- [x] `fclose()`
- [x] `fprintf()`
- [x] `fscanf()`
- [x] `fgets()`
- [x] `fputs()`

#### Memory Functions (2)
- [x] `calloc()`
- [x] `realloc()`

#### Time Functions (4)
- [x] `time()`
- [x] `clock()`
- [x] `localtime()`
- [x] `strftime()`

**Estimated effort:** 1 hour (add to SyntaxHighlighter.cs)  
**Blocked by:** None  
**Priority:** Medium (polish)

---

### Help System (3 items)
| Item | Handler | Status |
|------|---------|--------|
| Help Contents (F1) | ✅ | ✅ |
| Help Index | ✅ | ✅ |
| Topic Search (Ctrl+F1) | ❌ | ⏳ |
| Previous Topic (Alt+F1) | ❌ | ⏳ |
| Help on Help | ❌ | ⏳ |

**Estimated effort:** 3-4 hours  
**Blocked by:** Help content database design  
**Priority:** Low (nice-to-have)

---

### Search Features (2 items)
| Item | Handler | Status |
|------|---------|--------|
| Find (Ctrl+F) | ✅ | ✅ |
| Replace (Ctrl+H) | ✅ | ✅ |
| Find Again | ✅ | ✅ |
| Find Previous | ✅ | ✅ |
| Find Procedure | ❌ | ⏳ |
| Find Error | ✅ (click in panel) | ✅ |

**Estimated effort:** 1-2 hours  
**Blocked by:** None  
**Priority:** Low (convenience feature)

---

### Compiler Enhancements (1 item)
- [x] Expose 30s timeout in Options > Compiler
  - [x] Add numeric input field in Compiler dialog
  - [x] Save setting to config file
  - [x] Apply to ProcessRunner

**Estimated effort:** 1 hour  
**Blocked by:** None  
**Priority:** Low

---

## 🟡 LOW PRIORITY (Polish / Nice-to-have)

### File Menu Stubs (2 items)
- [ ] Save All — Hook to save all open tabs
- [ ] Print — Print active file (may need dialog)

**Estimated effort:** 1-2 hours  
**Status:** ⏳

---

### Edit Menu Stubs (2 items)
- [ ] Copy Example — Stub with placeholder message
- [ ] Show Clipboard — Stub with placeholder message

**Estimated effort:** 0.5 hours  
**Status:** ⏳

---

### Window Management Menu (8 items)
| Item | Handler | Status | Effort |
|------|---------|--------|--------|
| Size/Move (Ctrl+F5) | ❌ | ⏳ | 3h |
| Zoom (F5) | ❌ | ⏳ | 2h |
| Tile | ❌ | ⏳ | 2h |
| Cascade | ❌ | ⏳ | 2h |
| Output Panel | ❌ | ⏳ | 3h |
| Watch Panel | ❌ | ⏳ | 3h |
| Register Panel | ❌ | ⏳ | 2h |
| Project/Notes | ❌ | ⏳ | 2h |

**Total effort:** 19+ hours  
**Blocked by:** Multi-window/panel architecture redesign  
**Priority:** Low (TabView covers most use cases)

---

### Compile Menu (1 item)
- [ ] Primary C File... — Select primary file for multi-file projects

**Estimated effort:** 1-2 hours  
**Status:** ⏳  
**Priority:** Low (single-file focus sufficient)

---

### System Menu Stubs (2 items)
- [ ] Clear Desktop — Stub with placeholder message
- [ ] Repaint Desktop — Stub with placeholder message

**Estimated effort:** 0.5 hours  
**Status:** ⏳

---

## 📊 Implementation Summary

### By Category
| Category | Total | Done | Pending | % Complete |
|----------|-------|------|---------|-------------|
| **Debug Menu** | 4 | 0 | 4 | 0% |
| **Project Menu** | 6 | 0 | 6 | 0% |
| **Options Menu** | 8 | 2 | 6 | 25% |
| **Run Menu** | 6 | 2 | 4 | 33% |
| **Syntax HL** | 50+ | 50+ | 0 | 100% |
| **Help System** | 5 | 2 | 3 | 40% |
| **Search** | 6 | 6 | 0 | 100% |
| **File Menu** | 2 | 0 | 2 | 0% |
| **Edit Menu** | 2 | 0 | 2 | 0% |
| **Window Mgmt** | 8 | 0 | 8 | 0% |
| **Compile Menu** | 1 | 0 | 1 | 0% |
| **System Menu** | 2 | 0 | 2 | 0% |

**Total: 100 items | 57 Complete (57%) | 43 Pending (43%)**

---

### By Priority & Effort
| Priority | Count | Est. Effort | Status |
|----------|-------|-------------|--------|
| 🔴 High | 8 | — | ✅ DONE |
| 🟠 Medium | 30+ | ~25 hours | ⏳ TODO |
| 🟡 Low | 8+ | ~20 hours | ⏳ TODO |

---

## 🎯 Recommended Implementation Order

### **Phase 1: Quick Wins** ✅ COMPLETE
- [x] Expand syntax highlighting (20+ C functions)
- [x] Add "Find Again" / "Find Previous" to Search menu
- [x] Expose compiler timeout in Options

**Impact:** High (visual polish, UX improvement)  
**Effort:** Low

---

### **Phase 2: Menu Completeness** (6-8 hours)
- [ ] Debug menu stubs (4 dialogs)
- [ ] Run menu stubs (Program Reset)
- [ ] Linker, Make, Environment dialogs

**Impact:** Medium (menu coverage)  
**Effort:** Medium

---

### **Phase 3: Feature Expansion** (15+ hours)
- [ ] Project system (`.PRJ` format)
- [ ] Help > Topic Search
- [ ] Output/Watch panels

**Impact:** Medium (advanced features)  
**Effort:** High

---

### **Phase 4: Polish** (20+ hours)
- [ ] Window tiling/cascading
- [ ] Complete Debug menu (actual debugging)
- [ ] Save/Retrieve options persistence

**Impact:** Low (convenience)  
**Effort:** High

---

## 📝 Notes

### Known Blockers
- **GDB integration** — Trace/Step (F7/F8) require external debugger
- **Project format** — Need `.PRJ` file spec for project system
- **Panel architecture** — Window/Output/Watch panels need multi-window redesign

### Testing Needed
- [ ] Memory profiling (check for leaks in long sessions)
- [ ] Large file handling (>10MB)
- [ ] Tab stress test (50+ tabs open)
- [ ] Compiler timeout behavior
- [ ] Menu keyboard navigation (Alt+key chains)

### Documentation Needed
- [ ] API docs for extending syntax highlighter
- [ ] Help content library (for F1, Ctrl+F1)
- [ ] Project file format specification
- [ ] Configuration file format

---

## 🚀 Getting Started

Pick a task from **Phase 1** to get started:

```bash
# Example: Add syntax highlighting for strlen, strcmp, etc.
# File: Services/SyntaxHighlighter.cs
# Lines: ~50 (in _keywords set)
# Time: 15-30 min
```

---

**Questions?** See [README.md](README.md) or create a GitHub Issue!
