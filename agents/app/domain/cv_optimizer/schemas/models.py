from typing import List, Optional
from pydantic import BaseModel, Field

class OptimizedSectionBase(BaseModel):
    """Base pour toutes les sections optimisées nécessitant une justification."""
    justification_reorder: str = Field(
        ..., 
        description="Justification détaillée du nouvel ordre de cet élément (pourquoi l'avoir placé ici par rapport à l'offre)"
    )
    justification_rewrite: str = Field(
        ..., 
        description="Explication des changements de formulation, des mots-clés ajoutés ou du style orienté résultat"
    )

class OptimizedExperience(OptimizedSectionBase):
    """Une expérience optimisée."""
    titre: str = Field(..., description="Intitulé du poste (Ne pas modifier le sens original)")
    entreprise: str = Field(..., description="Nom de l'entreprise")
    description_optimisee: str = Field(..., description="Description réécrite en langage orienté résultat (STAR)")

class OptimizedProject(OptimizedSectionBase):
    """Un projet optimisé."""
    titre: str = Field(..., description="Titre du projet")
    description_optimisee: str = Field(..., description="Description réécrite en langage orienté résultat (STAR)")
    technologies: List[str] = Field(default_factory=list, description="Liste des technos (mises en avant si pertinentes)")

class OptimizedFormation(OptimizedSectionBase):
    """Une formation optimisée (surtout réordonnée)."""
    diplome: str = Field(..., description="Nom du diplôme")
    etablissement: str = Field(..., description="Nom de l'école ou université")

class OptimizedCertification(OptimizedSectionBase):
    """Une certification optimisée (surtout réordonnée)."""
    nom: str = Field(..., description="Nom de la certification")
    organisme: str = Field(..., description="Organisme délivreur")

class OptimizedSummary(BaseModel):
    """Résumé du profil."""
    contenu: str = Field(..., description="Résumé réécrit pour accrocher le recruteur")
    justification_rewrite: str = Field(..., description="Pourquoi ce résumé met en valeur le candidat pour cette offre précise")

class OptimizedCVOutput(BaseModel):
    """Sortie finale de l'agent d'optimisation."""
    resume_optimise: OptimizedSummary
    experiences_optimisees: List[OptimizedExperience] = Field(default_factory=list)
    projets_optimises: List[OptimizedProject] = Field(default_factory=list)
    formations_optimisees: List[OptimizedFormation] = Field(default_factory=list)
    certifications_optimisees: List[OptimizedCertification] = Field(default_factory=list)
    
    competences_reordonnees: List[str] = Field(
        default_factory=list, 
        description="Liste des compétences triées par pertinence pour l'offre"
    )
    justification_competences: str = Field(
        ..., 
        description="Pourquoi ces compétences spécifiques ont été mises en premier"
    )
    
    global_justification: str = Field(
        ...,
        description="Un paragraphe expliquant la stratégie globale d'optimisation adoptée pour ce CV."
    )
