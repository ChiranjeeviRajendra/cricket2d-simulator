# Human mesh, morph and skin provenance

The files below are vendored assets from the [official MakeHuman repository](https://github.com/makehumancommunity/makehuman/tree/master/makehuman/data). The original OBJ and target headers are retained. The skeleton and skin JSON explicitly identify their license as CC0. Credits include Data Collection AB, Joel Palmius and Jonas Hauquier.

| Local file | Original path under `makehuman/data` |
| --- | --- |
| `makehuman-base.obj` | `3dobjs/base.obj` |
| `default.mhskel` | `rigs/default.mhskel` |
| `default_weights.mhw` | `rigs/default_weights.mhw` |
| `adult-male.target` | `targets/macrodetails/asian-male-young.target` |
| `male-average.target` | `targets/macrodetails/universal-male-young-averagemuscle-averageweight.target` |
| `male-athletic.target` | `targets/macrodetails/universal-male-young-maxmuscle-averageweight.target` |

MakeHuman's [licensing page](https://www.makehumancommunity.org/about/license.html) and [core-assets clarification](https://www.makehumancommunity.org/mpfb/faq/build_other_chargen.html) describe the CC0 asset licensing. See the [CC0 1.0 legal text](https://creativecommons.org/publicdomain/zero/1.0/legalcode).

The build-time adapter applies the male target at weight 1 and the average/athletic targets at weights 0.55/0.45, scales the figure to 1.8 m and retargets arm/leg segment lengths to the simulation. It exports the continuous body surface with four normalized influences per source vertex, merging the source skeleton weights into 18 runtime bones. Helper geometry supplies joint positions but is not rendered. Source hands and feet are replaced by closed gloves and cricket shoes. Remaining wrist skin weights are merged into the forearm so bat/glove rotation cannot twist the last forearm skin ring. The jersey and trousers receive a smoother, looser surface; pads, gloves, shoes, helmet/grille and bat are original generated geometry.

Only the GLB and rig metadata are runtime assets. No MakeHuman executable or addon is required. The user's photographs and GIF remain pose references and are not redistributed as game textures.
