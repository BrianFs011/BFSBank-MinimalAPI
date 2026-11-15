# BFSBank — Minimal API (.NET 11)

Parte integrante do ecossistema multi-repo **BFSBank**, explorando o paradigma de **Minimal APIs** no .NET 11 e C# 15 para microsserviços ágeis, com baixa alocação de memória e inicialização veloz.

---

## 🚀 Como Executar

### Pré-requisitos
* [.NET 11 SDK](https://dotnet.microsoft.com/download)

### 1. Confiar no certificado HTTPS de desenvolvimento (se ainda não feito)
```bash
dotnet dev-certs https --trust
```

### 2. Rodar a aplicação com perfil HTTPS
```bash
dotnet run --launch-profile https
```

* **URL HTTPS:** `https://localhost:7021`
* **Endpoint de teste:** `https://localhost:7021/weatherforecast`

### 3. Rodar a Suíte de Testes Unitários & Integração (xUnit)
```bash
dotnet test
```

---

## 🏛️ Características do Paradigma
* **Low Ceremony**: Mapeamento direto de rotas via `app.MapGet` sem sobrecarga de controladores reflexivos.
* **Runtime Async V2**: Redução de overhead em chamadas assíncronas do .NET 11.
* **Ideal para**: Endpoints públicos de alto throughput e microsserviços focados.

---

Desenvolvido por [Brian Ferreira da Silva](https://github.com/BrianFs011) — Portfólio de Engenharia.
