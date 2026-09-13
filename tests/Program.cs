using System.Numerics;
using HudWorkshop;
var count = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
PanelRect P(string id, float x, float y, float w, float h, float ox = 0, float oy = 0) => new(id, new(x,y), new(ox,oy), new(w,h));
var panels = new[] { P("a",100,200,80,40,10,6), P("b",220,300,120,60,-4,8), P("c",400,450,60,30,6,-2) };
var bounds = LayoutGeometry.Bounds(panels);
Check(bounds.Start == new Vector2(110,206) && bounds.Size == new Vector2(356,272), "Bounds include frame offsets and mixed sizes");
var translated = LayoutGeometry.Translate(panels, new(11.7f,-9.2f));
Check(translated["b"] - translated["a"] == panels[1].Position - panels[0].Position, "Translation preserves relative positions");
Check(translated["a"] == new Vector2(112,191), "Common integer translation");
for (var mode=0;mode<6;mode++) {
 var positions=LayoutGeometry.Align(panels,mode);
 var frames=panels.Select(p=>p with {Position=positions[p.Id]}).ToArray();
 float Edge(PanelRect p)=>mode<3?p.Start.X+p.Size.X*mode/2f:p.Start.Y+p.Size.Y*(mode-3)/2f;
 Check(frames.Max(Edge)-frames.Min(Edge)<=1,"Alignment "+mode);
 Check(frames.All(p=>mode<3?p.Position.Y==panels.First(o=>o.Id==p.Id).Position.Y:p.Position.X==panels.First(o=>o.Id==p.Id).Position.X),"Unchanged perpendicular axis "+mode);
}
foreach(var horizontal in new[]{true,false}) {
 var targets=LayoutGeometry.Distribute(panels,horizontal);
 Check(!targets.ContainsKey("a")&&!targets.ContainsKey("c"),"Distribution anchors endpoints");
 var moved=panels.Select(p=>targets.TryGetValue(p.Id,out var v)?p with {Position=v}:p).ToArray();
 float Start(PanelRect p)=>horizontal?p.Start.X:p.Start.Y;
 float End(PanelRect p)=>horizontal?p.End.X:p.End.Y;
 Check(Math.Abs((Start(moved[1])-End(moved[0]))-(Start(moved[2])-End(moved[1])))<=1,"Equal edge gaps with mixed sizes");
}
Check(LayoutGeometry.Distribute(panels.Take(2).ToArray(),true).Count==0,"Two panels cannot be distributed");
var overlaps=new[]{P("a",0,0,100,20),P("b",10,0,100,20),P("c",20,0,100,20)};
Check(LayoutGeometry.Distribute(overlaps,true)["b"].X==10,"Negative gaps supported");
// Independent expected screen anchors, including right/bottom and shrink/expand round trips.
var anchors = new[] { new Vector2(0,0), new Vector2(100,0), new Vector2(200,0), new Vector2(0,40), new Vector2(100,40), new Vector2(200,40), new Vector2(0,80), new Vector2(100,80), new Vector2(200,80) };
for (byte i=0;i<9;i++) {
 var delta=ScaleGeometry.AnchorCompensation(i,new(200,80),1,2);
 Check(delta==anchors[i],"Scale anchor "+i);
 Check(delta+ScaleGeometry.AnchorCompensation(i,new(200,80),2,1)==Vector2.Zero,"Reversible scale anchor "+i);
}
try { ScaleGeometry.AnchorCompensation(9,new(10,10),1,2); throw new Exception("Accepted invalid anchor"); }
catch(ArgumentOutOfRangeException) { count++; }
Check(ScaleGeometry.AnchorCompensation(7,new(600,60),new(300,120))==new Vector2(-150,60),"Shape change preserves bottom-centre anchor");
Check(ScaleGeometry.AnchorCompensation(8,new(300,120),new(600,60))==new Vector2(300,-60),"Shape change preserves bottom-right anchor");
var targetIds=new[]{"_TargetInfo","_TargetInfoMainTarget","_TargetInfoCastBar","_TargetInfoBuffDebuff"};
Check(targetIds.Where(id=>TargetMode.Includes(id,false)).SequenceEqual(new[]{"_TargetInfo"}),"Combined mode exposes only combined target frame");
Check(targetIds.Where(id=>TargetMode.Includes(id,true)).SequenceEqual(targetIds.Skip(1)),"Split mode exposes exactly three target frames");
Check(TargetMode.Includes("_FocusTargetInfo",false)&&TargetMode.Includes("_FocusTargetInfo",true),"Focus target remains independent of target mode");
Check(OfflineGeometry.Origin(new(50,100),new(400,100),1,7,new(1920,1080))==new Vector2(760,980),"Offline bottom-centre location");
Check(OfflineGeometry.Origin(new(100,100),new(200,80),1.5f,8,new(1920,1080))==new Vector2(1620,960),"Offline scaled bottom-right location");
Check(OfflineGeometry.Origin(new(10,20),new(200,80),1,0,new(1920,1080))==new Vector2(192,216),"Offline top-left location");
Check(OfflineGeometry.EnabledByte(2,true)==3 && OfflineGeometry.EnabledByte(3,false)==2,"Toggle preserves gamepad visibility bit");
Check(OfflineGeometry.EnabledByte(254,true)==255 && OfflineGeometry.EnabledByte(255,false)==254,"Toggle preserves other visibility flags");
Console.WriteLine($"Passed {count} geometry checks.");

var shared = new SharedLayout(1, 3440, 1440, [new SharedElement("_ActionBar", 1234, 1111, 1.2f, false, 3, null), new SharedElement("ChatLog", 18, 1116, null, true, null, null)]);
var code = LayoutCode.Encode(shared);
var decoded = LayoutCode.Decode("  " + code + "\n");
Check(decoded.Width == shared.Width && decoded.Height == shared.Height && decoded.Elements.SequenceEqual(shared.Elements), "Share round trip preserves semantic fields");
void RejectCode(Action action, string name) {
    try { action(); } catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or InvalidDataException) { count++; return; }
    throw new Exception("Accepted invalid code: " + name);
}
RejectCode(() => LayoutCode.Decode(code[..^1] + (code[^1] == '0' ? '1' : '0')), "Checksum corruption");
RejectCode(() => LayoutCode.Decode(code.Replace("HUDW1:", "HUDW2:")), "Unknown prefix");
RejectCode(() => LayoutCode.Decode("HUDW1:not-base64:bad"), "Invalid base64");
RejectCode(() => LayoutCode.Decode(new string('x', LayoutCode.MaxText + 1)), "Oversized input");
RejectCode(() => LayoutCode.Encode(shared with { Version = 2 }), "Unknown schema");
RejectCode(() => LayoutCode.Encode(shared with { Elements = [shared.Elements[0], shared.Elements[0]] }), "Duplicate IDs");
RejectCode(() => LayoutCode.Encode(shared with { Elements = [shared.Elements[0] with { X = float.NaN }] }), "Non-finite coordinate");
RejectCode(() => LayoutCode.Encode(shared with { Elements = [shared.Elements[0] with { Shape = 6 }] }), "Unsafe shape");
RejectCode(() => LayoutCode.Encode(shared with { Elements = [shared.Elements[0] with { Scale = 9 }] }), "Unsafe scale");
RejectCode(() => LayoutCode.Encode(shared with { Elements = [shared.Elements[0] with { Simple = true }] }), "Conflicting semantic properties");
RejectCode(() => LayoutCode.Encode(shared with { Elements = [] }), "Empty layout");
// Valid checksum on a compressed expansion bomb must still be rejected before JSON decoding.
using (var bombBuffer = new MemoryStream()) {
 using (var zip = new System.IO.Compression.GZipStream(bombBuffer, System.IO.Compression.CompressionLevel.SmallestSize, true)) zip.Write(new byte[200000]);
 var bomb = bombBuffer.ToArray();
 RejectCode(() => LayoutCode.Decode("HUDW1:" + Convert.ToBase64String(bomb) + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bomb))), "Bounded decompression");
}
Console.WriteLine($"Passed {count} total checks including layout codes.");
