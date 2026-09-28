// ── API: /api/profile (mirrors the backend Profile DTOs) ────────────────────

export interface PersonalInfoDto {
  nom?: string | null;
  prenom?: string | null;
  email?: string | null;
  telephone?: string | null;
  ville?: string | null;
  pays?: string | null;
  titrePoste?: string | null;
  photoUrl?: string | null;
  lienLinkedin?: string | null;
  lienGithub?: string | null;
  lienPortfolio?: string | null;
  resumeProfessionnel?: string | null;
  /** JSON of the custom section titles. */
  titresSections?: string | null;
}

export interface ExperienceDto {
  id?: string | null;
  entreprise?: string | null;
  poste?: string | null;
  dateDebut?: string | null;
  dateFin?: string | null;
  missions?: string | null;
  ville?: string | null;
  type?: string | null;
  taches: string[];
}

export interface FormationDto {
  id?: string | null;
  etablissement?: string | null;
  diplome?: string | null;
  annee: number;
  ville?: string | null;
  specialisation?: string | null;
  mention?: string | null;
  anneeFin?: number | null;
}

export interface ProjetDto {
  id?: string | null;
  titreProjet?: string | null;
  description?: string | null;
  technologiesUtilisees?: string | null;
  lienProjet?: string | null;
  dateRealisation?: string | null;
  demoUrl?: string | null;
  imageUrl?: string | null;
  isUniversity: boolean;
  taches: string[];
}

/** A skill, or a language when typeCompetence is "Langue". */
export interface CompetenceDto {
  id?: string | null;
  nom?: string | null;
  niveau: number;
  typeCompetence?: string | null;
}

export interface CertificationDto {
  id?: string | null;
  titre?: string | null;
  organisation?: string | null;
  dateObtention?: string | null;
  idCredential?: string | null;
  urlCredential?: string | null;
}

export interface FullProfileDto {
  personalInfo: PersonalInfoDto;
  objectif?: string | null;
  niveau?: string | null;
  secteur?: string | null;
  onboardingCompleted: boolean;
  experiences: ExperienceDto[];
  formations: FormationDto[];
  projets: ProjetDto[];
  competences: CompetenceDto[];
  certifications: CertificationDto[];
}

export interface KeywordDto {
  mot: string;
  categorie: string;
}

export interface MessageResponse {
  message: string;
}

export interface PhotoUploadResponse {
  photoUrl: string;
  objectKey?: string;
  message?: string;
}

export interface SignedPhotoUrlResponse {
  photoUrl?: string | null;
  objectKey?: string;
}

/** AI resume generation; degrades to an empty resume plus errors. */
export interface GenerateResumeResponse {
  resume: string;
  errors?: string[];
}
