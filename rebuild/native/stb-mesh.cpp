// STB Studio YFT editor; ragekit is MIT-licensed (RatPrez/ragekit).
#include <ragekit/fragment.h>
#include <cmath>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>
namespace fs=std::filesystem;
static rk::Bytes load(const fs::path& p){
  std::ifstream f(p,std::ios::binary|std::ios::ate);if(!f)throw std::runtime_error("YFT introuvable");
  auto s=f.tellg();if(s<16||s>268435456)throw std::runtime_error("Taille YFT invalide");
  rk::Bytes b((size_t)s);f.seekg(0);if(!f.read((char*)b.data(),s))throw std::runtime_error("Lecture impossible");
  if(std::string((const char*)b.data(),4)!="RSC7")throw std::runtime_error("YFT Legacy RSC7 attendu");return b;
}
template<typename T>static void wr(std::ofstream& f,const T& v){f.write((const char*)&v,sizeof(T));}
static void wrstr(std::ofstream& f,std::string s){if(s.size()>255)s.resize(255);uint8_t n=(uint8_t)s.size();wr(f,n);f.write(s.data(),n);}
static std::vector<rk::Geometry*> geometries(rk::Fragment& frag){
  if(!frag.drawable)throw std::runtime_error("Drawable principal absent");
  std::vector<rk::Geometry*> a;for(auto& m:frag.drawable->models(rk::Lod::High))
    for(auto& g:m.geometries)a.push_back(&g);return a;
}
static void scene(const fs::path& input,const fs::path& output){
  auto frag=rk::read_yft(load(input));auto g=geometries(frag);if(g.empty()||g.size()>10000)throw std::runtime_error("Aucun maillage LOD0");
  size_t vs=0,is=0;for(auto* p:g){vs+=p->vertex_count;is+=p->indices.size();}
  if(vs>750000||is>3000000)throw std::runtime_error("Maillage trop volumineux");
  if(fs::exists(output))throw std::runtime_error("Sortie deja existante");
  std::ofstream f(output,std::ios::binary);if(!f)throw std::runtime_error("Sortie impossible");
  f.write("STBMESH2",8);wr(f,(uint32_t)g.size());uint32_t id=0;
  for(const auto& m:frag.drawable->models(rk::Lod::High))for(const auto& q:m.geometries){
    wr(f,id++);wr(f,(uint8_t)(m.is_skinned()?1:0));std::string tex;
    if(frag.drawable->shader_group&&q.shader_index<frag.drawable->shader_group->shaders.size()){
      const auto& sh=frag.drawable->shader_group->shaders[q.shader_index];tex=sh.texture_of("DiffuseSampler");
      if(tex.empty())tex=sh.texture_of("TextureSampler");}
    wrstr(f,tex);wr(f,(uint32_t)q.vertex_count);wr(f,(uint32_t)q.indices.size());
    for(uint32_t i=0;i<q.vertex_count;i++){auto v=q.position(i);wr(f,v.x);wr(f,v.y);wr(f,v.z);
      rk::Vec2 uv{};if(q.layout.has(rk::VertexSemantic::TexCoord0))uv=q.uv(i);wr(f,uv.x);wr(f,uv.y);}
    for(uint16_t ix:q.indices)wr(f,ix);
  }
  if(!f)throw std::runtime_error("Ecriture scene incomplete");
  std::cout<<"SCENE "<<g.size()<<" "<<vs<<" "<<is<<"\n";
}
struct Edit{uint32_t g,v;rk::Vec3 d;};
static void vertices(const fs::path& input,const fs::path& output,const fs::path& changes){
  if(fs::absolute(input).lexically_normal()==fs::absolute(output).lexically_normal()||fs::exists(output))
    throw std::runtime_error("Protege l'original : selectionne une sortie non existante");
  std::ifstream f(changes);if(!f)throw std::runtime_error("Modifications introuvables");
  std::vector<Edit> edits;std::string line;
  while(std::getline(f,line)){
    if(line.empty()||line[0]=='#')continue;
    Edit e{};std::string group,extra;std::istringstream ss(line);
    if(!(ss>>group>>e.v>>e.d.x>>e.d.y>>e.d.z)||ss>>extra||group.rfind("g:",0)!=0)
      throw std::runtime_error("Edition invalide");
    auto parsed=std::stoul(group.substr(2));if(parsed>UINT32_MAX)throw std::runtime_error("Groupe invalide");e.g=(uint32_t)parsed;
    if(!std::isfinite(e.d.x)||!std::isfinite(e.d.y)||!std::isfinite(e.d.z)||
       std::abs(e.d.x)>10||std::abs(e.d.y)>10||std::abs(e.d.z)>10)throw std::runtime_error("Delta hors limites");
    edits.push_back(e);if(edits.size()>10000)throw std::runtime_error("Trop de modifications");
  }
  if(edits.empty())throw std::runtime_error("Aucune modification");
  auto frag=rk::read_yft(load(input));auto mesh=geometries(frag);
  for(const auto& e:edits)if(e.g>=mesh.size()||e.v>=mesh[e.g]->vertex_count)throw std::runtime_error("Sommet inconnu");
  for(const auto& e:edits){auto& q=*mesh[e.g];auto p=q.position(e.v);
    q.set(e.v,rk::VertexSemantic::Position,{p.x+e.d.x,p.y+e.d.y,p.z+e.d.z,1});}
  for(auto& m:frag.drawable->models(rk::Lod::High)){for(auto& q:m.geometries)q.compute_bounds();m.compute_bounds();}
  frag.drawable->compute_bounds();frag.sphere_center=frag.drawable->bounding_center;
  frag.sphere_radius=frag.drawable->bounding_radius;
  auto bytes=rk::write_yft(frag);auto checked=rk::read_yft(bytes);
  if(geometries(checked).size()!=mesh.size())throw std::runtime_error("Verification YFT echouee");
  std::ofstream out(output,std::ios::binary);if(!out||!out.write((char*)bytes.data(),bytes.size()))throw std::runtime_error("Export impossible");
  std::cout<<"OK "<<edits.size()<<" "<<bytes.size()<<"\n";
}
int main(int argc,char**argv){try{
  if(argc==4&&std::string(argv[1])=="scene"){scene(argv[2],argv[3]);return 0;}
  if(argc==5&&std::string(argv[1])=="vertices"){vertices(argv[2],argv[3],argv[4]);return 0;}
  std::cerr<<"Usage: stb-mesh scene in.yft out.stbmesh | vertices in.yft out.yft changes.txt\n";return 2;
}catch(const std::exception& e){std::cerr<<"STB mesh: "<<e.what()<<"\n";return 1;}}
