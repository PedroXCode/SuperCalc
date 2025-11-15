#include <iostream>
#include <iomanip>
#include <string>
#include <vector>
#include <unordered_map>
#include <functional>
#include <algorithm>
#include <cctype>
#include <cmath>
#include <sstream>

using namespace std;

// --- Utilidades de string ---
static inline string ltrim(string s){ s.erase(s.begin(), find_if(s.begin(), s.end(), [](unsigned char c){return !isspace(c);})); return s; }
static inline string rtrim(string s){ s.erase(find_if(s.rbegin(), s.rend(), [](unsigned char c){return !isspace(c);} ).base(), s.end()); return s; }
static inline string trim(string s){ return ltrim(rtrim(s)); }

// --- Tokenización ---
enum class TokType { Number, Ident, LParen, RParen, Comma, Plus, Minus, Star, Slash, Caret, Assign, End };
struct Token{ TokType t; double value{}; string text; };

struct Lexer {
    const string s; size_t i=0, n;
    Lexer(string src): s(move(src)), n(s.size()) {}

    static bool isIdentStart(char c){ return isalpha((unsigned char)c) || c=='_'; }
    static bool isIdentChar(char c){ return isalnum((unsigned char)c) || c=='_'; }

    Token next(){
        while(i<n && isspace((unsigned char)s[i])) ++i;
        if(i>=n) return {TokType::End,0,""};
        char c = s[i];
        // números (incluye . y notación científica)
        if (isdigit((unsigned char)c) || c=='.'){
            size_t start=i; bool seenDot = (c=='.');
            ++i;
            while(i<n && (isdigit((unsigned char)s[i]) || (!seenDot && s[i]=='.'))){ if(s[i]=='.') seenDot=true; ++i; }
            // notación científica
            if(i<n && (s[i]=='e' || s[i]=='E')){
                size_t j=i+1; if(j<n && (s[j]=='+' || s[j]=='-')) ++j; bool any=false;
                while(j<n && isdigit((unsigned char)s[j])){ any=true; ++j; }
                if(any) i=j; // consume si es válido
            }
            double val = strtod(s.substr(start, i-start).c_str(), nullptr);
            return {TokType::Number, val, ""};
        }
        if (isIdentStart(c)){
            size_t start=i++; while(i<n && isIdentChar(s[i])) ++i;
            return {TokType::Ident, 0.0, s.substr(start, i-start)};
        }
        ++i; // un solo char
        switch(c){
            case '(': return {TokType::LParen,0,"("};
            case ')': return {TokType::RParen,0,")"};
            case ',': return {TokType::Comma,0,","};
            case '+': return {TokType::Plus,0,"+"};
            case '-': return {TokType::Minus,0,"-"};
            case '*': return {TokType::Star,0,"*"};
            case '/': return {TokType::Slash,0,"/"};
            case '^': return {TokType::Caret,0,"^"};
            case '=': return {TokType::Assign,0,"="};
            default: throw runtime_error(string("Símbolo inválido: ")+c);
        }
    }
};

// --- Shunting-yard a RPN ---
struct OpInfo { int prec; bool rightAssoc; int arity; };

static const unordered_map<string, OpInfo> OP = {
    {"+", {1,false,2}}, {"-", {1,false,2}}, {"*", {2,false,2}}, {"/", {2,false,2}}, {"^", {3,true,2}},
    {"u-", {4,true,1}} // menos unario
};

struct Node { // token para la RPN
    enum Kind{KNum, KVar, KOp, KFunc, KArgSep, KAssign} k;
    double val{}; string text; int argc{};
};

static bool isOpTok(TokType t){ return t==TokType::Plus||t==TokType::Minus||t==TokType::Star||t==TokType::Slash||t==TokType::Caret; }

vector<Node> toRPN(const string& line){
    Lexer L(line); vector<Node> output; vector<Node> ops;
    Token prev{TokType::End};
    for(Token tok = L.next(); tok.t!=TokType::End; tok=L.next()){
        if(tok.t==TokType::Number){ output.push_back({Node::KNum, tok.value}); }
        else if(tok.t==TokType::Ident){ output.push_back({Node::KVar, 0, tok.text}); }
        else if(tok.t==TokType::Comma){ // separador de argumentos
            while(!ops.empty() && ops.back().k!=Node::KArgSep && !(ops.back().k==Node::KOp && ops.back().text=="(") ){
                output.push_back(ops.back()); ops.pop_back();
            }
            if(ops.empty()) throw runtime_error("Coma fuera de contexto");
            output.push_back({Node::KArgSep});
        }
        else if(tok.t==TokType::LParen){ ops.push_back({Node::KOp,0,"("}); }
        else if(tok.t==TokType::RParen){
            while(!ops.empty() && !(ops.back().k==Node::KOp && ops.back().text=="(")){
                output.push_back(ops.back()); ops.pop_back();
            }
            if(ops.empty()) throw runtime_error("Paréntesis desbalanceados");
            ops.pop_back(); // quita '('
            if(!ops.empty() && ops.back().k==Node::KFunc){ output.push_back(ops.back()); ops.pop_back(); }
        }
        else if(tok.t==TokType::Assign){ ops.push_back({Node::KAssign}); }
        else if(isOpTok(tok.t)){
            string sym = (tok.t==TokType::Plus?"+": tok.t==TokType::Minus?"-": tok.t==TokType::Star?"*": tok.t==TokType::Slash?"/":"^");
            bool unary = (prev.t==TokType::End || prev.t==TokType::LParen || prev.t==TokType::Comma || isOpTok(prev.t) || prev.t==TokType::Assign);
            if(unary && sym=="-") sym = "u-";
            auto oi = OP.at(sym);
            while(!ops.empty() && (ops.back().k==Node::KOp||ops.back().k==Node::KFunc)){
                OpInfo top; bool isTopOp=false;
                if(ops.back().k==Node::KOp && OP.count(ops.back().text)){ top=OP.at(ops.back().text); isTopOp=true; }
                if(isTopOp && ( (oi.rightAssoc? oi.prec<top.prec : oi.prec<=top.prec) )){ output.push_back(ops.back()); ops.pop_back(); }
                else break;
            }
            ops.push_back({Node::KOp,0,sym});
        }
        else{
            throw runtime_error("Token inesperado");
        }
        prev = tok;
    }

    while(!ops.empty()){
        if(ops.back().k==Node::KOp && ops.back().text=="(") throw runtime_error("Paréntesis desbalanceados");
        output.push_back(ops.back()); ops.pop_back();
    }
    return output;
}

// --- Evaluador RPN con soporte de funciones/variables ---
struct Env{
    unordered_map<string,double> vars;
    int precision = 10;
    Env(){ vars["pi"]=acos(-1.0); vars["e"]=exp(1.0); }
};

using UFunc = function<double(double)>;
using BFunc = function<double(double,double)>;

static const unordered_map<string,UFunc> UF = {
    {"sin", (UFunc)[](double a){return sin(a);} }, {"cos", (UFunc)[](double a){return cos(a);} }, {"tan", (UFunc)[](double a){return tan(a);} },
    {"asin", (UFunc)[](double a){return asin(a);} }, {"acos", (UFunc)[](double a){return acos(a);} }, {"atan", (UFunc)[](double a){return atan(a);} },
    {"sqrt", (UFunc)[](double a){return sqrt(a);} }, {"cbrt", (UFunc)[](double a){return cbrt(a);} }, {"exp", (UFunc)[](double a){return exp(a);} },
    {"abs", (UFunc)[](double a){return fabs(a);} }, {"floor", (UFunc)[](double a){return floor(a);} }, {"ceil", (UFunc)[](double a){return ceil(a);} }, {"round", (UFunc)[](double a){return round(a);} },
    {"ln", (UFunc)[](double a){return log(a);} }, {"log", (UFunc)[](double a){return log(a);} }, {"log10", (UFunc)[](double a){return log10(a);} }
};

static const unordered_map<string,BFunc> BF = {
    {"pow", (BFunc)[](double a,double b){ return pow(a,b); }}
};

double applyOp(const string& op, const vector<double>& st){
    if(op=="u-") return -st.back();
    if(op=="+") return st[st.size()-2] + st.back();
    if(op=="-") return st[st.size()-2] - st.back();
    if(op=="*") return st[st.size()-2] * st.back();
    if(op=="/"){
        if(st.back()==0.0) throw runtime_error("División por cero");
        return st[st.size()-2] / st.back();
    }
    if(op=="^") return pow(st[st.size()-2], st.back());
    throw runtime_error("Operador desconocido: "+op);
}

// Evaluación con soporte de asignación simple: IDENT = expr
double evalRPN(const vector<Node>& rpn, Env& env){
    bool hasAssign=false; for(auto& n: rpn) if(n.k==Node::KAssign){ hasAssign=true; break; }

    if(hasAssign){
        if(rpn.size()<3 || rpn[0].k!=Node::KVar || rpn[1].k!=Node::KAssign)
            throw runtime_error("Asignación inválida. Usa: nombre = expresión");
        string name = rpn[0].text;
        vector<Node> rhs(rpn.begin()+2, rpn.end());
        vector<double> st;
        for(size_t i=0;i<rhs.size();++i){
            auto &n=rhs[i];
            if(n.k==Node::KNum) st.push_back(n.val);
            else if(n.k==Node::KVar){
                auto itF1 = UF.find(n.text);
                auto itF2 = BF.find(n.text);
                if(itF1!=UF.end()){
                    if(st.empty()) throw runtime_error("Falta argumento para función "+n.text);
                    double a = st.back(); st.pop_back();
                    st.push_back(itF1->second(a));
                } else if(itF2!=BF.end()){
                    if(st.size()<2) throw runtime_error("Faltan argumentos para función "+n.text);
                    double b = st.back(); st.pop_back();
                    double a = st.back(); st.pop_back();
                    st.push_back(itF2->second(a,b));
                } else {
                    auto itV = env.vars.find(n.text);
                    if(itV==env.vars.end()) throw runtime_error("Variable no definida: "+n.text);
                    st.push_back(itV->second);
                }
            }
            else if(n.k==Node::KOp){
                int need = (n.text=="u-"?1:2);
                if(st.size()< (size_t)need) throw runtime_error(string("Pila insuficiente (operador ")+n.text+")");
                double res = applyOp(n.text, st);
                for(int k=0;k<need;k++) st.pop_back();
                st.push_back(res);
            }
        }
        if(st.size()!=1) throw runtime_error("Expresión inválida en asignación");
        env.vars[name]=st.back();
        cout << "[ok] " << name << " = " << fixed << setprecision(env.precision) << st.back() << "\n";
        return st.back();
    }

    vector<double> st;
    for(const auto& n: rpn){
        if(n.k==Node::KNum){ st.push_back(n.val); }
        else if(n.k==Node::KVar){
            auto itF1 = UF.find(n.text);
            auto itF2 = BF.find(n.text);
            if(itF1!=UF.end()){
                if(st.empty()) throw runtime_error("Falta argumento para función "+n.text);
                double a = st.back(); st.pop_back();
                st.push_back(itF1->second(a));
            } else if(itF2!=BF.end()){
                if(st.size()<2) throw runtime_error("Faltan argumentos para función "+n.text);
                double b = st.back(); st.pop_back();
                double a = st.back(); st.pop_back();
                st.push_back(itF2->second(a,b));
            } else {
                auto itV = env.vars.find(n.text);
                if(itV==env.vars.end()) throw runtime_error("Variable no definida: "+n.text);
                st.push_back(itV->second);
            }
        }
        else if(n.k==Node::KOp){
            int need = (n.text=="u-"?1:2);
            if(st.size()< (size_t)need) throw runtime_error(string("Pila insuficiente (operador ")+n.text+")");
            double res = applyOp(n.text, st);
            for(int k=0;k<need;k++) st.pop_back();
            st.push_back(res);
        }
    }
    if(st.size()!=1) throw runtime_error("Expresión inválida");
    return st.back();
}

// --- Preprocesado ligero para llamadas a funciones a forma postfija ---
string preprocessFuncCalls(const string& in){
    string out; out.reserve(in.size()*2);
    for(size_t i=0;i<in.size();){
        if(isalpha((unsigned char)in[i])||in[i]=='_'){
            size_t j=i+1; while(j<in.size() && (isalnum((unsigned char)in[j])||in[j]=='_')) ++j;
            string name = in.substr(i, j-i);
            size_t k=j; while(k<in.size() && isspace((unsigned char)in[k])) ++k;
            if(k<in.size() && in[k]=='('){
                int depth=0; size_t m=k; vector<string> args; string cur;
                for(size_t p=k; p<in.size(); ++p){
                    char c=in[p];
                    if(c=='('){ depth++; if(depth>1) cur.push_back(c); }
                    else if(c==')'){
                        depth--; if(depth==0){ args.push_back(cur); m=p; break; } else cur.push_back(c);
                    }
                    else if(c==',' && depth==1){ args.push_back(cur); cur.clear(); }
                    else { cur.push_back(c); }
                }
                if(depth!=0) throw runtime_error("Paréntesis desbalanceados en llamada a función");
                out += "(";
                for(size_t a=0;a<args.size();++a){ out += args[a]; if(a+1<args.size()) out += " "; }
                out += ") "+name;
                i = m+1;
                continue;
            }
        }
        out.push_back(in[i]); ++i;
    }
    return out;
}

int main(){
    ios::sync_with_stdio(false); cin.tie(nullptr);

    Env env; cout << "SuperCalc++ (C++17). Escribe :help para ayuda. Ctrl+C/Ctrl+D para salir.\n";

    string line;
    while(true){
        cout << "> ";
        if(!getline(cin,line)) break;
        line = trim(line);
        if(line.empty()) continue;
        if(line==":quit") break;
        if(line==":help"){
            cout << "Comandos: :help, :vars, :clear, :precision N, :quit\n"
                 << "Funciones: sin, cos, tan, asin, acos, atan, sqrt, cbrt, log/ln, log10, exp, abs, floor, ceil, round, pow\n"
                 << "Constantes: pi, e\n"
                 << "Ejemplos: sin(pi/2), pow(2,8), x=5, 3*x^2 + 1\n";
            continue;
        }
        if(line==":vars"){
            for(auto &kv: env.vars){ cout << kv.first << " = " << fixed << setprecision(env.precision) << kv.second << "\n"; }
            continue;
        }
        if(line==":clear"){
            env.vars.clear(); env.vars["pi"]=acos(-1.0); env.vars["e"]=exp(1.0);
            cout << "[ok] variables limpiadas\n"; continue;
        }
        if(line.rfind(":precision",0)==0){
            istringstream iss(line.substr(10)); int p; if(iss>>p && p>=0 && p<=30){ env.precision=p; cout<<"[ok] precisión = "<<p<<"\n"; }
            else cout<<"Uso: :precision N (0..30)\n"; continue;
        }

        try{
            string pre = preprocessFuncCalls(line);
            auto rpn = toRPN(pre);
            double ans = evalRPN(rpn, env);
            cout << "= " << fixed << setprecision(env.precision) << ans << "\n";
        }catch(const exception& ex){
            cout << "[error] " << ex.what() << "\n";
        }
    }
    return 0;
}
