$userId = "3b52fba1-80a5-4cdd-aed5-a8da3e2e13c4"
$apiUrl = "http://localhost:5000/api/offers/submit"

$offerText = @"
Offre d'emploi : Développeur Full-Stack (Angular / .NET)
Entreprise : TechCorp Solutions
Lieu : Paris (Hybride)

Missions :
- Participer au développement et à l'architecture de notre nouvelle plateforme SaaS
- Coder le backend en C# .NET 8 (API REST, Entity Framework Core)
- Coder le frontend avec Angular 17+ et TypeScript
- Assurer la qualité du code (tests unitaires, CI/CD)
- Travailler en méthode Agile (Scrum) au sein d'une équipe de 5 personnes

Profil recherché :
- Au moins 3 ans d'expérience en développement web
- Maîtrise de C# .NET et de l'écosystème ASP.NET Core
- Solides compétences en Angular et TypeScript
- Connaissance des bases de données SQL (PostgreSQL ou SQL Server)
- Bonnes pratiques de code (Clean Code, SOLID)
- Esprit d'équipe et bonne communication
"@

$body = @{
    RawText = $offerText
    TemplateId = 1
} | ConvertTo-Json

$headers = @{
    "Content-Type" = "application/json"
    "X-User-Id" = $userId
}

Write-Host "Envoi de la soumission d'offre à l'API .NET pour l'utilisateur $userId..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $body -Headers $headers
    Write-Host "Soumission réussie ! OfferId: $($response.offerId)" -ForegroundColor Green
    Write-Host "Statut: $($response.status)" -ForegroundColor Yellow
    Write-Host "Le pipeline s'exécute en arrière-plan. Vous pouvez vérifier les logs des conteneurs 'backend' et 'agents'."
}
catch {
    Write-Host "Erreur lors de la soumission :" -ForegroundColor Red
    $_.Exception.Message
}
