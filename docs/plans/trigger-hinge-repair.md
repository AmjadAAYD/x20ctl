# Trigger hinge and independent-side repair

1. Inspect primary repair photographs and EasySMX's product information. Treat the trigger as a lever rotating around a fixed shaft; do not infer exact EasySMX dimensions from another controller.
2. Correct LT's mirrored path in source coordinates. All masks, cutouts, photographic layers and feedback must use the same positioned path. The previous renderer ignored the mirror, drawing both caps on RT and cancelling the duplicate even-odd cutout.
3. Replace whole-cap translation with projected rotation around a model-specific hinge at the body-side edge. Points on the shaft remain fixed. The finger/free edge has the largest displacement, driven continuously by input.
4. Prove the old LT issue with independently driven image comparisons. Verify mathematical hinge invariants, then render LT-only, RT-only, half/full/released states and both cycles for all seven models. Check the inactive side remains unchanged.
5. Build a separate local Windows executable; verify its bundled frontend. Do not launch the native window, use the user's screen/controller, publish, or push.

References: [iFixit Series controller trigger cap disassembly](https://www.ifixit.com/Guide/Xbox+Series+X%7CS+Wireless+Controller+(Model+1914)+Full+Disassembly/148234), step 7 photos show the body-side shaft and return spring; [EasySMX X20](https://www.easysmx.com/products/easysmx-x20-multiplatform-gaming-controller-with-trigger-lock-and-hall-effect-joysticks) confirms analog travel and two trigger-lock positions. The source photo angles and visual travel are approximations, not a measured hardware model.
