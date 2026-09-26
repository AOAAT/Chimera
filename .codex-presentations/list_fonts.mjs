import { GlobalFonts } from "@napi-rs/canvas";
console.log(JSON.stringify(GlobalFonts.families.map(x => x.family).sort(), null, 2));
