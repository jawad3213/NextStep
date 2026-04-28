using NextStep.data;
using NextStep.Modules.Profile.Models;
using NextStep.Modules.Profile.DTOs;
using NextStep.Modules.Identity.Models;
using NextStep.Modules.Identity.Services;
using Microsoft.EntityFrameworkCore;

namespace NextStep.Modules.Profile.Services
{
    public interface IProfileService
    {
        Task<FullProfileDto> GetFullProfileAsync(Guid userId);
        Task UpdatePersonalInfoAsync(Guid userId, PersonalInfoDto dto);
        
        Task AddExperienceAsync(Guid userId, ExperienceDto dto);
        Task UpdateExperienceAsync(Guid userId, ExperienceDto dto);
        Task DeleteExperienceAsync(Guid userId, Guid id);

        Task AddFormationAsync(Guid userId, FormationDto dto);
        Task UpdateFormationAsync(Guid userId, FormationDto dto);
        Task DeleteFormationAsync(Guid userId, Guid id);

        Task AddProjetAsync(Guid userId, ProjetDto dto);
        Task UpdateProjetAsync(Guid userId, ProjetDto dto);
        Task DeleteProjetAsync(Guid userId, Guid id);

        Task AddCompetenceAsync(Guid userId, CompetenceDto dto);
        Task UpdateCompetenceAsync(Guid userId, CompetenceDto dto);
        Task DeleteCompetenceAsync(Guid userId, Guid id);

        Task AddCertificationAsync(Guid userId, CertificationDto dto);
        Task UpdateCertificationAsync(Guid userId, CertificationDto dto);
        Task DeleteCertificationAsync(Guid userId, Guid id);

        Task CompleteOnboardingAsync(Guid userId, OnboardingDto dto);
    }

    public class ProfileService : IProfileService
    {
        private readonly AppDbContext _context;
        private readonly IUserService _userService;

        public ProfileService(AppDbContext context, IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        public async Task<FullProfileDto> GetFullProfileAsync(Guid userId)
        {
            var user = await _context.Utilisateurs.FindAsync(userId);
            if (user == null) throw new KeyNotFoundException("Utilisateur non trouvé.");

            return new FullProfileDto
            {
                PersonalInfo = new PersonalInfoDto
                {
                    Nom = user.Nom,
                    Prenom = user.Prenom,
                    Email = user.Email,
                    Telephone = user.Telephone,
                    Ville = user.Ville,
                    Pays = user.Pays,
                    TitrePoste = user.TitrePoste,
                    PhotoUrl = user.PhotoUrl,
                    LienLinkedin = user.LienLinkedin,
                    LienGithub = user.LienGithub,
                    LienPortfolio = user.LienPortfolio,
                    ResumeProfessionnel = user.ResumeProfessionnel,
                    TitresSections = user.TitresSections
                },
                Objectif = user.Objectif,
                Niveau = user.Niveau,
                Secteur = user.Secteur,
                OnboardingCompleted = user.OnboardingCompleted,
                Experiences = await _context.Experiences.Where(e => e.UserId == userId)
                    .Select(e => new ExperienceDto { 
                        Id = e.Id, 
                        Entreprise = e.Entreprise, 
                        Poste = e.Poste, 
                        DateDebut = e.DateDebut, 
                        DateFin = e.DateFin, 
                        Missions = e.Missions,
                        Ville = e.Ville,
                        Type = e.TypeContrat
                    })
                    .ToListAsync(),
                Formations = await _context.Formations.Where(f => f.UserId == userId)
                    .Select(f => new FormationDto { Id = f.Id, Etablissement = f.Etablissement, Diplome = f.Diplome, Annee = f.Annee, Ville = f.Ville, Specialisation = f.Specialisation, Mention = f.Mention, AnneeFin = f.AnneeFin })
                    .ToListAsync(),
                Projets = await _context.Projets.Where(p => p.UserId == userId)
                    .Select(p => new ProjetDto { 
                        Id = p.Id, 
                        TitreProjet = p.TitreProjet, 
                        Description = p.Description, 
                        TechnologiesUtilisees = p.TechnologiesUtilisees, 
                        LienProjet = p.LienProjet, 
                        DateRealisation = p.DateRealisation,
                        DemoUrl = p.DemoUrl,
                        ImageUrl = p.ImageUrl,
                        IsUniversity = p.IsUniversity
                    })
                    .ToListAsync(),
                Competences = await _context.Competences.Where(c => c.UserId == userId)
                    .Select(c => new CompetenceDto { Id = c.Id, Nom = c.Nom, Niveau = c.Niveau, TypeCompetence = c.TypeCompetence })
                    .ToListAsync(),
                Certifications = await _context.Certifications.Where(c => c.UserId == userId)
                    .Select(c => new CertificationDto { Id = c.Id, Titre = c.Titre, Organisation = c.Organisation, DateObtention = c.DateObtention, IdCredential = c.IdCredential, UrlCredential = c.UrlCredential })
                    .ToListAsync()
            };
        }

        public async Task UpdatePersonalInfoAsync(Guid userId, PersonalInfoDto dto)
        {
            var user = await _context.Utilisateurs.FindAsync(userId);
            if (user == null) throw new KeyNotFoundException("Utilisateur non trouvé.");

            user.Nom = dto.Nom;
            user.Prenom = dto.Prenom;
            user.Telephone = dto.Telephone;
            user.Ville = dto.Ville;
            user.Pays = dto.Pays;
            user.TitrePoste = dto.TitrePoste;
            user.PhotoUrl = dto.PhotoUrl;
            user.LienLinkedin = dto.LienLinkedin;
            user.LienGithub = dto.LienGithub;
            user.LienPortfolio = dto.LienPortfolio;
            user.ResumeProfessionnel = dto.ResumeProfessionnel;
            user.TitresSections = dto.TitresSections;

            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        // Experiences
        public async Task AddExperienceAsync(Guid userId, ExperienceDto dto)
        {
            var exp = new Experience { 
                UserId = userId, 
                Entreprise = dto.Entreprise, 
                Poste = dto.Poste, 
                DateDebut = dto.DateDebut, 
                DateFin = dto.DateFin, 
                Missions = dto.Missions,
                Ville = dto.Ville,
                TypeContrat = dto.Type
            };
            _context.Experiences.Add(exp);
            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        public async Task UpdateExperienceAsync(Guid userId, ExperienceDto dto)
        {
            var exp = await _context.Experiences.FirstOrDefaultAsync(e => e.Id == dto.Id && e.UserId == userId);
            if (exp == null) throw new KeyNotFoundException("Expérience non trouvée.");
            exp.Entreprise = dto.Entreprise; exp.Poste = dto.Poste; exp.DateDebut = dto.DateDebut; exp.DateFin = dto.DateFin; exp.Missions = dto.Missions;
            exp.Ville = dto.Ville; exp.TypeContrat = dto.Type;
            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        public async Task DeleteExperienceAsync(Guid userId, Guid id)
        {
            var exp = await _context.Experiences.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            if (exp != null) { _context.Experiences.Remove(exp); await _context.SaveChangesAsync(); await _userService.UpdateProfileScoreAsync(userId); }
        }

        // Projets
        public async Task AddProjetAsync(Guid userId, ProjetDto dto)
        {
            var p = new Projet { 
                UserId = userId, 
                TitreProjet = dto.TitreProjet, 
                Description = dto.Description, 
                TechnologiesUtilisees = dto.TechnologiesUtilisees, 
                LienProjet = dto.LienProjet, 
                DateRealisation = dto.DateRealisation,
                DemoUrl = dto.DemoUrl,
                ImageUrl = dto.ImageUrl,
                IsUniversity = dto.IsUniversity
            };
            _context.Projets.Add(p);
            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        public async Task UpdateProjetAsync(Guid userId, ProjetDto dto)
        {
            var p = await _context.Projets.FirstOrDefaultAsync(x => x.Id == dto.Id && x.UserId == userId);
            if (p == null) throw new KeyNotFoundException("Projet non trouvé.");
            p.TitreProjet = dto.TitreProjet; p.Description = dto.Description; p.TechnologiesUtilisees = dto.TechnologiesUtilisees; p.LienProjet = dto.LienProjet; p.DateRealisation = dto.DateRealisation;
            p.DemoUrl = dto.DemoUrl; p.ImageUrl = dto.ImageUrl; p.IsUniversity = dto.IsUniversity;
            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        public async Task DeleteProjetAsync(Guid userId, Guid id)
        {
            var p = await _context.Projets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (p != null) { _context.Projets.Remove(p); await _context.SaveChangesAsync(); await _userService.UpdateProfileScoreAsync(userId); }
        }

        // Competences
        public async Task AddCompetenceAsync(Guid userId, CompetenceDto dto)
        {
            var c = new Competence { UserId = userId, Nom = dto.Nom, Niveau = dto.Niveau, TypeCompetence = dto.TypeCompetence };
            _context.Competences.Add(c);
            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        public async Task UpdateCompetenceAsync(Guid userId, CompetenceDto dto)
        {
            var c = await _context.Competences.FirstOrDefaultAsync(x => x.Id == dto.Id && x.UserId == userId);
            if (c == null) throw new KeyNotFoundException("Compétence non trouvée.");
            c.Nom = dto.Nom; c.Niveau = dto.Niveau; c.TypeCompetence = dto.TypeCompetence;
            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        public async Task DeleteCompetenceAsync(Guid userId, Guid id)
        {
            var c = await _context.Competences.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (c != null) { _context.Competences.Remove(c); await _context.SaveChangesAsync(); await _userService.UpdateProfileScoreAsync(userId); }
        }

        // Certifications
        public async Task AddCertificationAsync(Guid userId, CertificationDto dto)
        {
            var cert = new Certification 
            { 
                UserId = userId, 
                Titre = dto.Titre, 
                Organisation = dto.Organisation, 
                DateObtention = dto.DateObtention, 
                IdCredential = dto.IdCredential, 
                UrlCredential = dto.UrlCredential 
            };
            _context.Certifications.Add(cert);
            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        public async Task UpdateCertificationAsync(Guid userId, CertificationDto dto)
        {
            var cert = await _context.Certifications.FirstOrDefaultAsync(x => x.Id == dto.Id && x.UserId == userId);
            if (cert == null) throw new KeyNotFoundException("Certification non trouvée.");
            
            cert.Titre = dto.Titre;
            cert.Organisation = dto.Organisation;
            cert.DateObtention = dto.DateObtention;
            cert.IdCredential = dto.IdCredential;
            cert.UrlCredential = dto.UrlCredential;

            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        public async Task DeleteCertificationAsync(Guid userId, Guid id)
        {
            var cert = await _context.Certifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (cert != null) 
            { 
                _context.Certifications.Remove(cert); 
                await _context.SaveChangesAsync(); 
                await _userService.UpdateProfileScoreAsync(userId); 
            }
        }

        // Formations
        public async Task AddFormationAsync(Guid userId, FormationDto dto)
        {
            var f = new Formation { UserId = userId, Etablissement = dto.Etablissement, Diplome = dto.Diplome, Annee = dto.Annee, Ville = dto.Ville, Specialisation = dto.Specialisation, Mention = dto.Mention, AnneeFin = dto.AnneeFin };
            _context.Formations.Add(f);
            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        public async Task UpdateFormationAsync(Guid userId, FormationDto dto)
        {
            var f = await _context.Formations.FirstOrDefaultAsync(x => x.Id == dto.Id && x.UserId == userId);
            if (f == null) throw new KeyNotFoundException("Formation non trouvée.");
            f.Etablissement = dto.Etablissement; f.Diplome = dto.Diplome; f.Annee = dto.Annee; f.Ville = dto.Ville; f.Specialisation = dto.Specialisation; f.Mention = dto.Mention; f.AnneeFin = dto.AnneeFin;
            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }

        public async Task DeleteFormationAsync(Guid userId, Guid id)
        {
            var f = await _context.Formations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (f != null) { _context.Formations.Remove(f); await _context.SaveChangesAsync(); await _userService.UpdateProfileScoreAsync(userId); }
        }

        public async Task CompleteOnboardingAsync(Guid userId, OnboardingDto dto)
        {
            var user = await _context.Utilisateurs.FindAsync(userId);
            if (user == null) throw new KeyNotFoundException("Utilisateur non trouvé.");

            user.Objectif = dto.Objectif;
            user.Niveau = dto.Niveau;
            user.Secteur = dto.Secteur;
            user.OnboardingCompleted = true;

            await _context.SaveChangesAsync();
            await _userService.UpdateProfileScoreAsync(userId);
        }
    }
}
