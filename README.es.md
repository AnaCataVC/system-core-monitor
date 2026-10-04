<p align="center">
  <img src="icon.png" alt="system-core-monitor Logo" width="120" />
</p>

# System Core Monitor

[English](README.md) | [Español](README.es.md)

<div align="center">


[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=flat&logo=windows)](https://www.microsoft.com/windows)
[![C# .NET](https://img.shields.io/badge/C%23%2013-WPF%20%2F%20.NET%209-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/Release-v3.3.0-93A8FD?style=flat)](https://github.com/AnaCataVC/system-core-monitor/releases/tag/v3.3.0)
[![Binary Size](https://img.shields.io/badge/Binary%20Size-687%20KB-success?style=flat)]()
[![Tests](https://img.shields.io/badge/Tests-43%20Passed-brightgreen?style=flat)]()
[![Antivirus](https://img.shields.io/badge/Antivirus-0%20False%20Positives-7EE7B8?style=flat)]()
[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat)](LICENSE)

*Centro de mando interactivo y panel de telemetría de alto rendimiento desarrollado exclusivamente en C# 13 compilado y Windows Presentation Foundation (.NET 9 WPF). Con cero dependencias externas, telemetría sub-milisegundo Win32 P/Invoke, Monitor de Sesiones de Agentes de IA y MCP, Motor de Retención y Limpieza de Transcripciones de IA, detección de procesos huérfanos con terminación de árbol segura ante reuso de PID, Cierre Ordenado en Dos Fases, control de procesos a nivel de kernel (NtSuspend/NtResume), planes de energía en 1 clic, limpieza endurecida multizona de almacenamiento, tarjetas métricas Bento interactivas, analítica multidisco responsiva, registro empresarial de fallos, maximización DPI multimonitor sin fisuras y huella de cero heurísticas en un binario autónomo de 687 KB.*

<br/>

<img src="mockup-preview.png" alt="System Core Monitor v3.0 Interface Mockup" width="900" style="border-radius: 14px; box-shadow: 0 20px 50px rgba(0,0,0,0.5);" />

</div>

---


### 1. Descripción del Proyecto
**System Core Monitor v3.3.0** es un centro de mando interactivo y panel de telemetría de alto rendimiento desarrollado exclusivamente en C# (.NET 9) y Windows Presentation Foundation (WPF). Monitorea y gestiona de forma activa los recursos críticos del sistema—**CPU, Memoria RAM, Almacenamiento Multidisco, Red y Latencia Ping, Procesos en Tiempo Real, Sesiones de Agentes de IA y Servidores MCP, Mantenimiento de Transcripciones y Logs de IA, Servicios de Windows, Tareas Programadas, Programas de Inicio y Aceleradores de Hardware (GPU/NPU)**—en un único ejecutable standalone de **687 KB** sin dependencias NuGet externas.

---

### 2. ⚡ Guía de Botones de Acción y Centro de Mando

System Core Monitor evoluciona de un monitor pasivo a un **Centro de Mando Activo**. A continuación se detalla el comportamiento exacto y la API nativa detrás de cada control interactivo:

| Botón / Acción | Ubicación en UI | API Win32 / Kernel Utilizada | Comportamiento y Propósito Exacto |
| :--- | :--- | :--- | :--- |
| **🚀 Modo Turbo** | Ribbon Superior / Bandeja | Win32 `PowrProf.dll` (`PowerSetActiveScheme`) + `EmptyWorkingSet` | Activa al instante el plan de energía de **Alto Rendimiento** de Windows (desestaciona núcleos de CPU) y ejecuta simultáneamente una purga agresiva del *working set* de memoria RAM en procesos de usuario. |
| **🤖 Monitor de Agentes IA & MCP** | Pestaña Agentes IA | Win32 `CreateToolhelp32Snapshot` + `SafeProcessHandle` + `_sampleGate` + Guarda PID Reuse + Resiliencia Snapshot | Detecta herramientas CLI de IA (`claude.exe`, `gemini.exe`, `codex.exe`, `aider.exe`, `ollama.exe`, `cursor.exe`, `antigravity.exe`), identifica sesiones CLI reanudadas por hash/UUID (`--resume=`, `🔗 Sesión <8-char-hash>`), visualiza badges dinámicos del Modelo de IA (`--model`, `🧬 <ModelName>`), desacopla subprocesos totales (`ChildProcessCount`) de servidores MCP verificados (`McpServersCount`), aísla lanzadores efímeros (`npx`, `uvx`), detecta servidores MCP compilados en Go/Rust por flags CLI, promueve sesiones CLI independientes con corte de fronteras en el árbol, blinda el vaciado de cachés ante fallos de snapshot (`allRunningPids.Count > 0`), y refleja dinámicamente estados Activo (Esmeralda `#10B981`) vs Inactivo (Pizarra `#64748B`). |
| **🗄️ Transcripts IA** | Ribbon Superior / Pestaña IA | `AiTranscriptMaintenance` + Filtro de Retención (>7 días) | Escaneo seguro y auditoría de almacenamiento para transcripciones y logs de sesiones de CLI de IA (Claude Code, Gemini CLI). Purga selectiva con umbral configurable y exclusión de subagentes activos sin inspección de contenido. |
| **🛑 Cierre Ordenado en Dos Fases** | Pestañas Procesos e IA | `CloseMainWindow` / `WM_CLOSE` + Detección Tray | **Fase 1**: Envío no bloqueante de solicitud de cierre ordenado y detección inteligente de minimizado a la Bandeja del Sistema (`MainWindowHandle == IntPtr.Zero`). **Fase 2**: Confirmación para forzar cierre solo si continúa activo o colgado. |
| **⚡ Terminar Árbol (Tree Kill)** | Pestaña Agentes IA | Terminación Topológica Inversa + Compuerta de Identidad `(PID, StartTime)` | Finaliza árboles de procesos completos en orden topológico inverso (subprocesos MCP primero $\rightarrow$ proceso raíz al final), evitando procesos huérfanos zombis. El recorrido se niega a descender hacia un proceso que arrancó *antes* que su padre registrado, de modo que un proceso vivo que solo heredó un PID de padre reciclado nunca se arrastra dentro del árbol ajeno. |
| **🧟 Detección y Limpieza de Huérfanos** | Pestaña Agentes IA | Diferencia de Reclamo Toolhelp32 + Prueba de Padre Muerto/Reasignado + Edad Mínima | Lista los procesos de runtime (`python`, `node`, `docker`, shells…) que **ninguna sesión de agente viva reclama** y cuyo padre ya no existe o fue reasignado a otro proceso — lo que deja atrás una sesión interrumpida, por ejemplo un `multiprocessing.Pool` cuyos workers sobrevivieron a la corrida. Windows no tiene un recolector de huérfanos, así que se acumulan en silencio entre reintentos. Cada fila muestra el motivo de detección, la antigüedad y la RAM, con la línea de comandos saneada en el tooltip; nada se termina automáticamente, y tanto la acción por fila como "Terminar Todos" revalidan el `StartTime` de cada PID antes de matar. |
| **🌐 Vaciar DNS** | Ribbon Superior | Nativa `dnsapi.dll` (`DnsFlushResolverCache`) | Purga y reinicia la caché del solucionador de nombres DNS de Windows en 0.01 ms, corrigiendo errores de navegación y resolución de dominios sin abrir CMD. |
| **🧹 Limpiar Temporales** | Ribbon Superior | Multizona `SafeTempCleaner` (>24h Cutoff) | Limpieza segura de archivos temporales en `%TEMP%`, `C:\Windows\Temp`, `WinSxS\Temp`, `SoftwareDistribution\Download` y `DeliveryOptimization`. Blindado con **aislamiento de Junctions/Symlinks** y **Guarda de Doble Marca de Tiempo** (`CreationTime` + `LastWriteTime`). |
| **🔍 Escanear Almacenamiento** | Pestaña Discos | `FolderSizeScanner` + `IProgress<T>` / `CancellationToken` | Mide el tamaño recursivo de cada carpeta de primer nivel bajo una raíz elegida y ordena las más pesadas, respondiendo *dónde* se fue el espacio en vez de solo cuán lleno está el volumen. Los subárboles se recorren en paralelo, el progreso se limita a un reporte cada 150 ms y el escaneo se puede cancelar a mitad de camino. Los reparse points se reportan como omitidos y **nunca aportan bytes**. |
| **🔎 Análisis de Bloat** | Pestaña Discos | `BloatDetector` + `SHQueryRecycleBin` | Expone los grandes consumidores que una barra por volumen no puede mostrar: discos virtuales Docker/WSL (VHDX) que crecieron, cachés de compilación regenerables (Gradle, NuGet, npm, pip, Maven, Android), la papelera, los archivos de paginación/hibernación y el almacén de componentes WinSxS — cada uno clasificado por severidad y con una remediación concreta. |
| **🧹 Limpiar Caché** | Fila de Bloat | `SafeTempCleaner.CleanWhitelistedCache` validado por whitelist | Borra una caché regenerable solo tras una **coincidencia exacta contra una whitelist explícita**; cualquier ruta arbitraria se rechaza. Hereda la guarda de antigüedad de doble marca de tiempo y nunca sigue junctions. Los elementos del sistema no exponen acción de borrado. |
| **⚠️ Rescatar Proceso** | Alerta en Barra de Título | Watchdog Win32 `IsResponding` + Cierre en Dos Fases | Detecta en vivo procesos con ventanas que no responden a la cola de mensajes de Windows (`IsResponding == false`). El botón *"Rescatar"* ejecuta el protocolo seguro de cierre en dos fases. |
| **⏸️ Suspender Proceso** | Lista de Procesos / Contexto | Kernel `ntdll.dll` (`NtSuspendProcess`) | Congela todos los hilos de ejecución de un proceso desbocado, reduciendo su consumo de CPU al 0.0% instantáneamente sin cerrarlo ni perder el trabajo abierto. |
| **▶️ Resume Process** | Lista de Procesos / Contexto | Kernel `ntdll.dll` (`NtResumeProcess`) | Reactiva un proceso previamente suspendido, devolviendo sus hilos al planificador de tareas de Windows. |
| **🚨 Reanudar Todos** | Barra de Pestaña Procesos | Batch `NtResumeProcess` Watchdog | Botón de seguridad global que descongela simultáneamente todos los procesos de usuario suspendidos. |
| **⚡ Prioridad de CPU** | Menú Contextual | Win32 `ProcessPriorityClass` (`SetPriorityClass`) | Modifica la prioridad del proceso en el planificador del procesador (`Tiempo Real`, `Alta`, `Normal`, `Baja`, `Inactiva`) para priorizar juegos o renderizados. |
| **🔍 Búsqueda en Vivo** | Barra de Pestaña Procesos | Filtro Reactivo con Debounce de 200 ms | Filtra al instante por ejecutable, PID o nombre comercial de la aplicación sin congelar la interfaz. |
| **⚡ Ordenación Rápida en Memoria** | Barra de Pestaña Procesos | `ApplyProcessSortingFast` + `_syncLock` | Alterna instantáneamente entre orden descendente por CPU % y RAM (MB) en memoria sin bloquear la interfaz ni repetir escaneos del sistema operativo. |
| **🎛️ Tarjetas Bento Clicables** | Panel Principal (HUD) | Enrutamiento de Eventos WPF | Al hacer clic en cualquier tarjeta (CPU, RAM, GPU, Disco, Red) redirige a la pestaña de detalle y aplica el filtro relevante. |
| **🧮 Carga de CPU por Núcleo** | Tarjeta Bento de CPU | Kernel `ntdll.dll` (`NtQuerySystemInformation`, `SystemProcessorPerformanceInformation`) | Muestra una barra de carga por procesador lógico y cuántos núcleos están realmente en uso (ocupados ≥ 10% del intervalo), no solo el % total. |
| **⚡ Optimizar RAM** | Tarjeta Memoria / Bandeja | Win32 `SetProcessWorkingSetSize` / `EmptyWorkingSet` | Vierte las páginas de memoria no referenciadas del proceso físico al archivo de paginación y ejecuta recolección de basura CLR. |
| **📌 Fijar Ventana (Pin)** | Barra de Título | Propiedad `Topmost` de WPF | Mantiene la ventana por encima de juegos o aplicaciones a pantalla completa para monitorización continua. |
| **🗖 Maximizado Preciso** | Controles de Ventana | Hook Nativo Win32 `WM_GETMINMAXINFO` | Intercepta el mensaje `0x0024` y calcula el área de trabajo mediante `MonitorFromWindow`, evitando que la ventana tape la barra de tareas o se desborde en múltiples pantallas. |
| **🎨 4 Temas Visuales** | Paleta en Barra de Título | `ActivePillActionButtonStyle` y Recursos Dinámicos | Alterna al instante entre **Pastel Oscuro**, **Pastel Claro**, **Cyberpunk Neón** y **Sakura Rosa** con botones vectoriales de estilo adaptativo. |

> [!TIP]
> 📖 **Manual Técnico y de Seguridad en Detalle:** Si deseas consultar los diagramas de secuencia Mermaid, invariantes de llamadas nativas P/Invoke y el blindaje TOCTOU/Junctions, revisa el **[Manual Técnico del Centro de Mando](docs/command-center-guide.md)**.

---

### 3. Características Nativas Destacadas:
- **🤖 Monitor de Agentes IA & Servidores MCP:** Detección en tiempo real de sesiones de herramientas de IA CLI y subprocesos MCP hijos con métricas desacopladas, identificación de sesiones reanudadas por hash (`--resume=`, `🔗 Sesión <8-char-hash>`), badge condicional de modelo (`--model`, `🧬 <ModelName>`) con DataTriggers resistentes a null/cadenas vacías, blindaje de vaciado de caché Toolhelp32 (`allRunningPids.Count > 0`), tolerancia a cold-start de PEB y badges dinámicos de estado Activo/Idle.
- **🗄️ Mantenimiento de Transcripciones y Logs de IA:** Auditoría y purga no invasiva de historiales JSONL acumulados por herramientas de IA CLI (Claude Code, Gemini CLI) con protección de sesiones recientes (>7 días), exclusión de subagentes activos y preservación de privacidad.
- **⚡ Arquitectura Cero Fugas de Handles y Cerrojo Anti-Reentrada:** Disposición determinista de handles Win32 (`SafeProcessHandle` mediante `using`/`Dispose()`) y cerrojo anti-reentrada `_sampleGate` para cálculo estable de deltas de CPU bajo alta frecuencia de muestreo.
- **🛡️ Protocolo de Cierre en Dos Fases:** Cierre seguro y no bloqueante con detección de aplicaciones minimizadas a la Bandeja del Sistema (*System Tray*) y terminación topológica inversa.
- **🧟 Detección de Huérfanos sin Matar Solo:** Expone los procesos de runtime que dejó atrás una sesión de agente interrumpida — sin reclamar por ninguna sesión viva, con el padre muerto o reasignado, y más antiguos que la ventana de gracia de arranque — y deja la decisión en tus manos, mostrando el motivo, la antigüedad, la RAM y la línea de comandos saneada detrás de una confirmación explícita.
- **Diseño Bento a Ancho Completo:** Cuadrícula de alta densidad sin márgenes muertos, optimizada para resoluciones modernas.
- **Registro Resiliente de Fallos (`CrashLogger.cs`):** Captura global de excepciones en `AppDomain`, `TaskScheduler` y `Dispatcher` con rotación automática a 1MB y límite de tasa de 5 registros cada 10 segundos.
- **Centro de Almacenamiento Multidisco:** Visualizador en tiempo real de unidades de disco (NVMe/SSD/HDD), estado de salud, espacio libre y accesos directos al Explorador de archivos.
- **🔍 Analizador de Almacenamiento y Detección de Bloat Oculto:** Desglose de tamaño por carpeta bajo demanda, con recorrido paralelo de subárboles, progreso limitado y cancelación a mitad de camino, más la detección de los consumidores de espacio que una barra de uso no puede revelar por diseño — discos virtuales Docker/WSL que crecieron, cachés de compilación regenerables, la papelera, y los archivos de paginación/hibernación etiquetados como del sistema para que nadie los persiga en vano.
- **☁️ Desambiguación de Unidades en la Nube:** Las unidades virtuales que Windows reporta como discos fijos (Google Drive y similares) se marcan con un badge y se excluyen de todo cálculo de almacenamiento, evitando que un montaje que refleja el volumen anfitrión duplique la capacidad real del equipo.
- **🔍 Inspector 360° de Procesos:** Identificación amigable de nombres comerciales, publicadores certificados, arquitectura y memoria con 0ms de retardo.
- **Lista Negra de Protección del Sistema:** Protección estricta que previene la suspensión o cierre de procesos vitales del sistema (`csrss`, `dwm`, `svchost`, `explorer`, `services`, `lsass`).
- **Pipeline de CI/CD Automatizado:** Compilación y ejecución de 30 tests automatizados de salud, transcripciones de IA, estrés en vivo y arquitectura en cada release.

---

### 4. Arquitectura y Estructura Modular

```text
system-core-monitor/
├── src/
│   ├── SystemCoreMonitor.csproj    # Archivo de proyecto C# WPF (.NET 9 SDK-style)
│   ├── App.xaml & App.xaml.cs      # Punto de entrada, bootstrap de CrashLogger y selector de 4 temas
│   ├── app.manifest                # Manifiesto de DPI V2 por monitor y compatibilidad Windows 10/11
│   ├── Core/                       # Motor del núcleo, llamadas Win32 P/Invoke y guardas de seguridad (24 módulos)
│   │   ├── NativeMethods.cs        # Win32 & NT kernel P/Invoke (ntdll, user32, dnsapi, powrprof, toolhelp32)
│   │   ├── CrashLogger.cs          # Logging resiliente de fallos (tope de 1MB, rotación y límite de tasa)
│   │   ├── PowerPlanManager.cs     # Conmutador nativo de planes de energía (Equilibrado, Alto Rendimiento, Ahorro)
│   │   ├── ProcessManager.cs       # Cierre ordenado en dos fases, árbol de terminación seguro contra reuso de PID
│   │   ├── ProcessMetadataCache.cs # Caché de metadatos de alto rendimiento con 0ms de latencia
│   │   ├── SafeTempCleaner.cs      # Limpiador multizona blindado (anti-TOCTOU y a prueba de Junctions)
│   │   ├── FileSystemSafety.cs     # Guarda compartida de reparse points y clasificador de unidades virtuales
│   │   ├── FolderSizeScanner.cs    # Desglose cancelable de carpetas con reporte de progreso estrangulado
│   │   ├── BloatDetector.cs        # Detector de grandes consumidores ocultos y limpieza con whitelist exacta
│   │   ├── AiTranscriptCleaner.cs  # Auditor seguro de transcripciones CLI de IA con retención configurable
│   │   ├── MemoryOptimizer.cs      # Compactador de working set de RAM y recolector de basura CLR
│   │   ├── SnapshotExporter.cs     # Generador de diagnósticos e informes del sistema en Markdown
│   │   ├── ConfigManager.cs        # Persistencia de configuraciones de usuario en %APPDATA%
│   │   ├── ToolLauncher.cs         # Lanzadores seguros de herramientas de diagnóstico de Windows
│   │   ├── WindowsAcceleratorEngine.cs # Optimizador de respuesta del sistema y telemetría
│   │   ├── CpuUsageTracker.cs      # Rastreador compartido de deltas de CPU indexado por PID
│   │   ├── TimedCache.cs           # Envoltorio genérico de caché con caducidad temporal
│   │   ├── MetricFormatting.cs     # Formateador reutilizable de bytes y umbrales porcentuales
│   │   ├── DxgiHelper.cs           # Telemetría gráfica DirectX DXGI y VRAM dedicada
│   │   ├── SetupApiHelper.cs       # Descubrimiento de hardware de unidades de procesamiento neuronal (NPU)
│   │   ├── LocalizationManager.cs  # Proveedor de localización dinámica en tiempo real (ES/EN)
│   │   ├── TrayManager.cs          # Controlador del icono y menú de la bandeja del sistema
│   │   ├── StartupHelper.cs        # Gestor de registro de aplicaciones de inicio de Windows
│   │   └── WindowPlacementHelper.cs # Persistencia geométrica de estado y coordenadas de ventana
│   ├── Models/                     # Modelos fuertemente tipados de telemetría y sesiones
│   │   ├── SystemMetrics.cs        # DTOs de telemetría de hardware y modelos de procesos
│   │   ├── AiAgentSession.cs       # Modelos de sesión de Agentes IA y jerarquía de subprocesos MCP
│   │   └── AiTranscriptModels.cs   # Modelos de escaneo y retención de transcripciones de IA
│   ├── Modules/                    # Recolectores autónomos de telemetría (12 recolectores)
│   │   ├── CpuCollector.cs         # Cálculo delta de CPU vía GetSystemTimes + carga por núcleo (NtQuerySystemInformation)
│   │   ├── MemoryCollector.cs      # Métricas de memoria física y archivo de paginación con GlobalMemoryStatusEx
│   │   ├── DiskCollector.cs        # Evaluador de volúmenes de disco y unidades físicas
│   │   ├── NetworkCollector.cs     # Tráfico de red en vivo Rx/Tx y latencia ping ICMP
│   │   ├── ProcessCollector.cs     # Muestreo seguro de procesos con bloqueo de sincronía y ordenación rápida
│   │   ├── AiAgentCollector.cs     # Escáner Toolhelp32, agregador de sesiones MCP y detector de huérfanos
│   │   ├── GpuCollector.cs         # Carga de motor gráfico y telemetría de GPU vía DXGI
│   │   ├── NpuCollector.cs         # Descubrimiento y monitoreo de aceleradores NPU
│   │   ├── ServiceCollector.cs     # Censo y control de servicios del sistema Windows
│   │   ├── TaskCollector.cs        # Enumerador de tareas programadas de Windows
│   │   ├── StartupCollector.cs     # Enumerador de aplicaciones de inicio y claves de registro
│   │   └── HardwareCollector.cs    # Estado de batería, tiempo de actividad y especificaciones
│   ├── ViewModels/                 # Capa de presentación desacoplada MVVM
│   │   ├── ViewModelBase.cs        # Clase base ObservableObject con INotifyPropertyChanged
│   │   ├── MainViewModel.cs        # Orquestador principal del panel
│   │   ├── DashboardViewModel.cs   # Enlace de datos de las tarjetas Bento del HUD
│   │   ├── AiAgentsViewModel.cs    # Enlace de sesiones de IA y árbol de servidores MCP
│   │   ├── ProcessesViewModel.cs   # Tabla de procesos con filtrado y ordenación rápida
│   │   ├── StorageViewModel.cs     # Enlace de visualización de almacenamiento y limpieza de bloat
│   │   ├── ServicesViewModel.cs    # Gestión de servicios de Windows y tareas programadas
│   │   ├── StartupViewModel.cs     # Inspección de aplicaciones de inicio
│   │   ├── AcceleratorsViewModel.cs # Aceleradores del sistema y descarte de telemetría
│   │   └── SettingsViewModel.cs    # Configuración, temas y selección de idioma
│   ├── Views/                      # Vistas XAML modulares
│   │   ├── DashboardView.xaml      # Vista interactiva del HUD Bento
│   │   ├── AiAgentsView.xaml       # Vista de sesiones de agentes y subprocesos MCP
│   │   ├── ProcessesView.xaml      # Tabla de procesos con búsqueda y ordenación en memoria
│   │   ├── StorageView.xaml        # Visualizador de discos y limpieza de bloat
│   │   ├── ServicesView.xaml       # Vista de servicios y tareas programadas
│   │   ├── StartupView.xaml        # Administrador de aplicaciones de inicio
│   │   ├── AcceleratorsView.xaml   # Aceleradores del sistema y optimizaciones de Windows
│   │   └── SettingsView.xaml       # Vista de configuración y selección de paleta
│   └── UI/                         # Diálogos modales, vectores y recursos gráficos
│       ├── MainWindow.xaml & .cs   # Ventana contenedor, barra de acciones y hook WM_GETMINMAXINFO
│       ├── ProcessDetailsWindow.xaml & .cs # Inspector modal 360° de procesos
│       ├── Converters/             # Conversores de valores XAML (Brushes, anchos, unidades)
│       ├── Icons/VectorIcons.xaml  # Iconografía vectorial en alta resolución
│       └── Themes/                 # Paletas Pastel Dark, Light, Neon, Rose y CommonStyles
├── scripts/
│   └── Build-Package.ps1           # Compilación single-file .NET 9 y generación de instalador
├── tests/
│   ├── Metrics.Tests.ps1           # Suite de validación de tipos y salud (19 pruebas)
│   ├── AiTranscript.Tests.ps1      # Suite de retención y limpieza de transcripciones de IA (5 pruebas)
│   └── DeepStress.Tests.ps1        # Pruebas de estrés, guarda de PID, fugas de handles y smoke (6 pruebas)
├── docs/                           # Guías de arquitectura, centro de mando y benchmarks
└── releases/                       # Binario autónomo, instalador Setup y archivo ZIP portátil (gitignored)
```

---

### 5. Instrucciones de Compilación y Ejecución

#### Ejecutar Directamente:
```powershell
.\releases\SystemCoreMonitor.exe
```

#### Compilar desde el Código Fuente:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Package.ps1 -Version "v3.0.0"
```

#### Ejecutar Pruebas Automatizadas (30 Tests):
Todas las suites cargan el binario desde `releases/` mediante el runtime de .NET 9, por lo que en un clon nuevo hay que compilar el paquete primero.
```powershell
# 1. Pruebas de Salud y Arquitectura (19 tests)
pwsh -ExecutionPolicy Bypass -File .\tests\Metrics.Tests.ps1

# 2. Pruebas de Mantenimiento de Transcripciones de IA (5 tests)
pwsh -ExecutionPolicy Bypass -File .\tests\AiTranscript.Tests.ps1

# 3. Pruebas de Estrés en Vivo, Guarda de Reuso de PID, Fugas de Handles y Smoke (6 tests)
pwsh -ExecutionPolicy Bypass -File .\tests\DeepStress.Tests.ps1
```

---

## 📄 License
MIT License. Free for personal and commercial use.


