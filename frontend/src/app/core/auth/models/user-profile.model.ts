
export interface UserProfile {
  id: string;
  keycloakId: string;
  nom: string | null;
  prenom: string | null;
  email: string;
  lienLinkedin?: string | null;
  lienGithub?: string | null;
  lienPortfolio?: string | null;
  resumeProfessionnel?: string | null;
  coordonnees?: string | null;
  dateInscription: string;
}

export interface UserProfileResponse {
  message: string;
  data: UserProfile;
}
