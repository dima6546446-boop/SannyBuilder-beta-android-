import os, json, glob
ROOT="/home/user/SannyBuilder-beta-android-/RussianDriftUnity/Assets/Scripts"
OUT="/tmp/claude-0/chk"
UNITY=glob.glob("/tmp/claude-0/ref/lib/net35/*.dll")
UI="/tmp/claude-0/ref/ui/lib/UnityEngine.UI.dll"
def proj(name, files_glob, refs, extra_dlls, editor=False):
    items="\n".join(f'<Reference Include="{os.path.basename(d)[:-4]}"><HintPath>{d}</HintPath></Reference>' for d in extra_dlls)
    prefs="\n".join(f'<ProjectReference Include="{OUT}/{r}/{r}.csproj"/>' for r in refs)
    return f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net35</TargetFramework><LangVersion>9.0</LangVersion><Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems><GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AssemblyName>{name}</AssemblyName><NoWarn>CS0649;CS0414;CS0169;CS0219;CS1701;CS1702;CS8632;CS0618;CS0067</NoWarn>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors><DefineConstants>UNITY_EDITOR;UNITY_ANDROID</DefineConstants>
  </PropertyGroup>
  <ItemGroup><PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies.net35" Version="1.0.3" PrivateAssets="all" /></ItemGroup>
  <ItemGroup>{items}</ItemGroup>
  <ItemGroup>{prefs}</ItemGroup>
  <ItemGroup><Compile Include="{files_glob}" /></ItemGroup>
</Project>'''
# stubs
for sname,sfile in (("Unity.InputSystem","stubs/InputSystemStub.cs"),("Unity.RenderPipelines.Universal.Runtime","stubs/UrpStub.cs")):
    os.makedirs(f"{OUT}/{sname}",exist_ok=True)
    open(f"{OUT}/{sname}/{sname}.csproj","w").write(proj(sname,f"{OUT}/{sfile}",[],UNITY+[UI]))
alias={"Unity.RenderPipelines.Core.Runtime":"Unity.RenderPipelines.Universal.Runtime","Unity.RenderPipelines.Core.Editor":"Unity.RenderPipelines.Universal.Runtime","Unity.RenderPipelines.Universal.Editor":"Unity.RenderPipelines.Universal.Runtime"}
EDSTUB=True
mods=[]
for d in sorted(glob.glob(ROOT+"/*/")):
    asm=glob.glob(d+"*.asmdef")
    if not asm: continue
    j=json.load(open(asm[0])); name=j["name"]
    refs=[]
    for r in j["references"]:
        r=alias.get(r,r)
        if r=="UnityEngine.UI": continue
        if r not in refs: refs.append(r)
    dlls=list(UNITY)+[UI]
    os.makedirs(f"{OUT}/{name}",exist_ok=True)
    files=d+"**/*.cs"
    if name=="RussianDrift.Editor": files=d+"**/*.cs;"+OUT+"/stubs/EditorStub.cs"
    open(f"{OUT}/{name}/{name}.csproj","w").write(proj(name,files,refs,dlls))
    mods.append(name)
open(f"{OUT}/mods.txt","w").write("\n".join(mods))
print(mods)
