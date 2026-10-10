# Solid trigger rotation

The previous implementation transformed a photographic sheet with no side walls. Replace only the moving caps with a textured, beveled 3D extrusion of the existing model-specific control contour. Use a fixed hinge and rigid rotation toward the viewer. Retain the controller body, socket image, interaction targets and independent analog inputs. Add Three.js for geometry triangulation, bevel generation and GPU rendering; dispose resources on unmount and render only when inputs/size change. Keep the released photo as a fallback if WebGL is unavailable. Verify actual nonzero mesh depth, rigid distances, hinge anchoring, continuous and independent input, stable released registration, and narrow/wide rendering in an isolated browser. No native screen/controller use, publishing or hardware writes.

Thickness and angle remain visual approximations, not measured hardware/CAD data.
