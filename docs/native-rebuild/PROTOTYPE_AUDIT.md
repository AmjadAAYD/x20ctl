# Prototype file disposition audit

Date: 8 October 2026. Completed BEFORE new shell implementation. Scope: all 42 authored source/config/asset files returned by rg --files desktop-dotnet; generated bin/obj/TestResults excluded.

Existing prototype is a preserved migration artifact. No old screen/theme is authoritative. DISCARD AFTER REPLACEMENT is a future disposition, not a deletion performed now. No prototype file is changed by this milestone. REUSE denotes development-only engineering reuse, not hardware verification.

Checkpoint: C:/Users/amjad/.codex/checkpoints/x20ctl-native-rebuild/20261008T205718Z-controller-zone/source.zip. 570 source files; exact member size/SHA-256 verification passed at creation. Archive SHA-256: 5869544715A2F83D628E22EBE8E814A0DB3C95F50F437067FC022D1CF0624687. Original Git index, status and binary tracked diff also preserved. Ignored private research/vendor/captures untouched; this is not an exhaustive backup of ignored material.

Baseline: dotnet test X20Ctl.Desktop.Tests -c Release --no-restore: 24 passed, 0 failed, no hardware. Input endpoints/holds/source/reconnect/foreground/mouse/navigation examined. Native GUI/hardware acceptance is not established by those tests.

New product is a separate X20Ctl.Product project; old solution/entry point remain unchanged. Original repository X20 illustration and logo are reused from src/assets; scanner-specific cutout is not used. Archive/backups and prototype source remain available.

| Path relative to desktop-dotnet | Disposition | Reason / evidence |
| --- | --- | --- |
| global.json | REUSE AS-IS | SDK 10.0.400 pin retained; installed SDK verified. |
| X20Ctl.sln | KEEP ONLY AS REFERENCE | Prototype solution stays unchanged; separate Product.sln prevents accidental Tester startup. |
| X20Ctl.Simulation/X20Ctl.Simulation.csproj | REUSE AS-IS | Development-only dependency for tested synthetic input/navigation; no hardware API. |
| X20Ctl.Simulation/InputFrame.cs | REUSE AS-IS | Explicit simulation provenance and raw ranges; 24 existing tests pass. |
| X20Ctl.Simulation/Navigation.cs | REUSE AS-IS | Fresh edges, foreground/capture suppression, source/reconnect baseline, repeat/hysteresis tested; wrapped in explicit context. |
| X20Ctl.Simulation/PointerMode.cs | REUSE AS-IS | Intentional-movement threshold tested; no visual composition. |
| X20Ctl.Simulation/PressTimeline.cs | KEEP ONLY AS REFERENCE | Useful simulated duration behavior; not needed in Zone and not scanner hardware parity. |
| X20Ctl.Desktop.Tests/X20Ctl.Desktop.Tests.csproj | REFACTOR / ADAPT | Reuse package/test conventions in separate test project; no new framework. |
| X20Ctl.Desktop.Tests/InputBehaviorTests.cs | KEEP ONLY AS REFERENCE | Existing 24-test suite verifies simulation only; no product layout/hardware proof. |
| X20Ctl.Desktop.Tests/LifecycleTests.cs | KEEP ONLY AS REFERENCE | Retain reconnect/source/mouse checks; add focused Zone ownership tests separately. |
| X20Ctl.Desktop/X20Ctl.Desktop.csproj | REFACTOR / ADAPT | Carry .NET 10 WPF WinExe/icon/resource setup into independent product project. |
| X20Ctl.Desktop/AssemblyInfo.cs | REFACTOR / ADAPT | Standard theme assembly metadata; no product-specific value. |
| X20Ctl.Desktop/App.xaml | DISCARD AFTER REPLACEMENT | Prototype theme merge is superseded; retain file unchanged now. |
| X20Ctl.Desktop/App.xaml.cs | DISCARD AFTER REPLACEMENT | Prototype startup/review/scanner routing is superseded; never product startup. |
| X20Ctl.Desktop/MainWindow.xaml | DISCARD AFTER REPLACEMENT | Tester-first screen and disabled Studio tabs must not become product shell. |
| X20Ctl.Desktop/MainWindow.xaml.cs | DISCARD AFTER REPLACEMENT | InputWorkspace/ScannerWindow ownership cannot carry into new Zone. |
| X20Ctl.Desktop/ScannerWindow.xaml | DISCARD AFTER REPLACEMENT | Scanner-first composition is superseded; no merged themes or screen reuse. |
| X20Ctl.Desktop/ScannerWindow.xaml.cs | KEEP ONLY AS REFERENCE | Window lifecycle/drawer observations useful later, not Zone composition. |
| X20Ctl.Desktop/ConsoleWindow.cs | REFACTOR / ADAPT | WindowChrome/DWM/frame concepts useful; remove IInputWorkspace and capture coupling in new independent frame. |
| X20Ctl.Desktop/Views/IInputWorkspace.cs | KEEP ONLY AS REFERENCE | Tester-specific contract; no place in Zone product. |
| X20Ctl.Desktop/Views/InputWorkspace.xaml | DISCARD AFTER REPLACEMENT | Old controller/telemetry split superseded; not visual template. |
| X20Ctl.Desktop/Views/InputWorkspace.xaml.cs | KEEP ONLY AS REFERENCE | Simulation lifecycle/integrity-checked scanner launch only; no product hardware reuse. |
| X20Ctl.Desktop/Views/ScannerInputsPage.xaml | DISCARD AFTER REPLACEMENT | Old Scanner screen cannot be foundation or first deliverable. |
| X20Ctl.Desktop/Views/ScannerInputsPage.xaml.cs | KEEP ONLY AS REFERENCE | Simulator/drawer/timer behavior; native capture later belongs to C++ engine. |
| X20Ctl.Desktop/Development/Review.cs | REFACTOR / ADAPT | Retain built-WPF render/binding-check technique; new Zone assertions and honest DPI labels. |
| X20Ctl.Desktop/Development/ScannerVisualReview.cs | KEEP ONLY AS REFERENCE | Scanner-specific render harness; explicitly not actual OS screenshot evidence. |
| X20Ctl.Desktop/Assets/controller-scanner-cutout-v1.png | KEEP ONLY AS REFERENCE | Inspected image has its own background/missing stick caps; not new art foundation. |
| X20Ctl.Desktop/ViewModels/WorkspaceModel.cs | KEEP ONLY AS REFERENCE | Tester/scanner synthetic telemetry state; new Zone model independent. |
| X20Ctl.Desktop/Themes/X20Colors.xaml | DISCARD AFTER REPLACEMENT | Old style values are not authoritative; new product resources independently authored. |
| X20Ctl.Desktop/Themes/X20Controls.xaml | DISCARD AFTER REPLACEMENT | Old control templates/visual styling superseded; accessibility requirements retained independently. |
| X20Ctl.Desktop/Themes/ScannerSceneTheme.xaml | DISCARD AFTER REPLACEMENT | Scanner scene palette/composition must not flow into Zone. |
| X20Ctl.Desktop/Controls/Atmosphere.xaml | DISCARD AFTER REPLACEMENT | Author new environmental lighting from master brief. |
| X20Ctl.Desktop/Controls/Atmosphere.xaml.cs | DISCARD AFTER REPLACEMENT | No useful nonvisual behavior beyond old view constructor. |
| X20Ctl.Desktop/Controls/ScannerEnvironment.xaml | DISCARD AFTER REPLACEMENT | Scanner atmosphere specifically superseded. |
| X20Ctl.Desktop/Controls/ScannerEnvironment.xaml.cs | DISCARD AFTER REPLACEMENT | Old scene constructor only. |
| X20Ctl.Desktop/Controls/TitleBar.xaml | DISCARD AFTER REPLACEMENT | Scanner header/link and old visual styling superseded. |
| X20Ctl.Desktop/Controls/TitleBar.xaml.cs | REFACTOR / ADAPT | Use standard native window command concepts; discard scanner-specific routing. |
| X20Ctl.Desktop/Controls/ControllerVisualizer.cs | REFACTOR / ADAPT | Repository art/clip geometry concept useful; no telemetry renderer in Zone and no scanner asset reuse. |
| X20Ctl.Desktop/Controls/AnalogBar.cs | KEEP ONLY AS REFERENCE | Potential future presentation meter; not authoritative capture. |
| X20Ctl.Desktop/Controls/StickPlot.cs | KEEP ONLY AS REFERENCE | Future visualization experiment only; no evidence clock or new Zone telemetry. |
| X20Ctl.Desktop/Controls/TriggerMeter.xaml | KEEP ONLY AS REFERENCE | Future control experiment; no retention of current screen composition. |
| X20Ctl.Desktop/Controls/TriggerMeter.xaml.cs | KEEP ONLY AS REFERENCE | Future value presentation only; no hardware ownership. |

## Frozen prototype hashes

| File | SHA-256 before implementation |
| --- | --- |
| X20Ctl.sln | B97A217EEB87A81E9F1BD36151370664C3B87C2F1A1F3B5D4F5FB5EB04A0B912 |
| X20Ctl.Simulation/X20Ctl.Simulation.csproj | 61E3F2D043E37F0DB4BE1D8083DD31BEBBFBB8FDE2CF027327DB2556C16BC385 |
| X20Ctl.Simulation/PressTimeline.cs | 3866DC1B12D38F00D4E4DFB8D5431EF0E60D078624D4D4E2B61883CC906F1153 |
| X20Ctl.Simulation/PointerMode.cs | 09F7A193E7789EF0AD42278A6D5A0A87E18800C7EF2FF91457118A9D86440F3B |
| X20Ctl.Simulation/Navigation.cs | 2BCCCBF3C1746426E17BA456294F92522E4C20F0D72EB838893C77B4E299BBD3 |
| X20Ctl.Simulation/InputFrame.cs | 50229B09A6942005BF2E0ACDE476BD20492CCF0AED9F0D89A81672B992AD3F01 |
| X20Ctl.Desktop.Tests/X20Ctl.Desktop.Tests.csproj | 1D9E669C2D8FFCE3F14D159196019F089257351C0C9F689DDFA3FF8AE2C412FE |
| X20Ctl.Desktop.Tests/LifecycleTests.cs | C33F31F229A143A0BDC7C23C950F0534453836DA881FCDF698B502CA3F6CB8F3 |
| X20Ctl.Desktop.Tests/InputBehaviorTests.cs | 3DED471CCB4AD79DC364F01B6A8DAC32F7213FBC301396D1673EC7557B67842D |
| X20Ctl.Desktop/X20Ctl.Desktop.csproj | 953933033B327B8537646FA19A816801225117151A7486CB176E3BF8042AE73A |
| X20Ctl.Desktop/Views/ScannerInputsPage.xaml.cs | 0E7063F494C8AB825ECFB37673731B1247525201885349449080D44077748A40 |
| X20Ctl.Desktop/Views/ScannerInputsPage.xaml | 4880AA16EFA8AF9F9A3DAD976B296FEDD0685E5B0A287CF3DC9777E91A3D7F79 |
| X20Ctl.Desktop/Views/InputWorkspace.xaml.cs | 548D86EB26D752345CA80C90DBD82BE761435E36C0C5F94DB1263BABAC87FA52 |
| X20Ctl.Desktop/Views/InputWorkspace.xaml | 1C3BCE730F0CB17D691050D4D31C684AA8CAF1569629985FC0D4CC11E6716096 |
| X20Ctl.Desktop/Views/IInputWorkspace.cs | B50A19AADA46D0E801D1847020148AB85890DEFE38704DC95F893226C80D24DF |
| X20Ctl.Desktop/ViewModels/WorkspaceModel.cs | 3AEC1997F80832D0D989DDFFFD37E26AF032002988DF14CB596A5F907146C673 |
| X20Ctl.Desktop/Themes/X20Controls.xaml | 54B29E9DADDC678CE82ACB2678FAAB54690233DE89D0DBA624D9589E16B2AEAB |
| X20Ctl.Desktop/Themes/X20Colors.xaml | 28ABFC605A5BC028CC619B5B025E32799229FA1DA83520D5826E529557481036 |
| X20Ctl.Desktop/Themes/ScannerSceneTheme.xaml | 9AA5030E84DA4E913FCE9CE73C08790B7816CD456C4B5F5BD05D657CE120C7A2 |
| X20Ctl.Desktop/ScannerWindow.xaml.cs | 3B2A85135C8325E1D111013C965299CA0F236F2A8D4B2B4C44CB7C2429C80CA7 |
| X20Ctl.Desktop/ScannerWindow.xaml | 027BFB3E9FF0A83F2A0AF810995140E52E77B58F32C81BA457826A4F0A9D4910 |
| X20Ctl.Desktop/MainWindow.xaml.cs | 07C8BAC9ACAF3A79ACC457AFB0DC6FF3DAA9FCB0476CBC85E0C4E8160B4656A5 |
| X20Ctl.Desktop/MainWindow.xaml | 0B677B608B841026D00C7E1E20B3F3572043D9DF2D030497A83C9748439A0094 |
| global.json | 309B86727E3D5DF3E0E8537228528FD311FE098775076B928F2941703C00F2EE |
| X20Ctl.Desktop/App.xaml.cs | 7483F08174B390D9051DE434D973B893EE4C6EA602922AB7A3C6B25F087D9F60 |
| X20Ctl.Desktop/App.xaml | 8FBC0B3F197B1ED82883ED07BBB486DEE3A2EFADAD7D17011AEB1DB51B210283 |
| X20Ctl.Desktop/AssemblyInfo.cs | 3FAC2AF92385F676E9A941BEFD23FE38F0A5FA6167D4A3A88F07E730C5595CA3 |
| X20Ctl.Desktop/ConsoleWindow.cs | AE8B5108A78FFE90163D05B33D63AA00D031D299F5C6E86CBCD79B6414C87181 |
| X20Ctl.Desktop/Development/ScannerVisualReview.cs | B70898E41AA34D12188BC809BC104BE5F4714115F4F186F8C782D8155F427F57 |
| X20Ctl.Desktop/Development/Review.cs | 5B97415D9794BD345F6D7FE8B5CE46880DCCEB27EBDA582D2BFCEAD0CA13067B |
| X20Ctl.Desktop/Assets/controller-scanner-cutout-v1.png | DAC77504CA6F0468D77052475B1977C2A68963AA211CA5807F7CEFFB033848D4 |
| X20Ctl.Desktop/Controls/AnalogBar.cs | E403C4132B2510EE3C0B63A5052C88A607858581461970BCA6858304D262B52D |
| X20Ctl.Desktop/Controls/Atmosphere.xaml | 25ED625E687330FD7BD569D89DCD8EA9F038BC6DB2D8A65A00B38B5114C0D5BC |
| X20Ctl.Desktop/Controls/Atmosphere.xaml.cs | C0C35BBFBBC82B67B2D07E3962B434F1772A3A7D0132A29EAB0377FEAD36AD48 |
| X20Ctl.Desktop/Controls/TriggerMeter.xaml.cs | 1602E063A0380F05FA36E6DFA1FDC2C858B6302F41E02B60065BAD43C7474DED |
| X20Ctl.Desktop/Controls/TriggerMeter.xaml | DAEBEF916CE0F5D987348FD3DDACA4F7CC8D1D9B79B83A31894C90D3CBDE78CD |
| X20Ctl.Desktop/Controls/TitleBar.xaml.cs | A3706F47E2B222064B65293B85551CED5D840FE30C630465F9AC683A83E8D71A |
| X20Ctl.Desktop/Controls/TitleBar.xaml | A63D1F52DB744EBC7106DFFE0441A63D6F10440B1967DAB1184D941884A9BE04 |
| X20Ctl.Desktop/Controls/StickPlot.cs | 5DFEA60BA8F9E8234533C11069E128F74905151EF3FDF620DA3C862FAC4B734F |
| X20Ctl.Desktop/Controls/ScannerEnvironment.xaml.cs | C7AA61BB743E9B8F775E8D7CBB69321B1AEA1FABDAD8236ACC5A97BF8AA3B142 |
| X20Ctl.Desktop/Controls/ScannerEnvironment.xaml | 53F770A1805FDE298752AFC78E1D0F7CC7C89D9E19F222552CC2B720023DD5D5 |
| X20Ctl.Desktop/Controls/ControllerVisualizer.cs | 4F18A5AD5CFA946BDBD79F42F3D7B510353C351866D2CE5DEDBFCEBBFCBCF916 |
