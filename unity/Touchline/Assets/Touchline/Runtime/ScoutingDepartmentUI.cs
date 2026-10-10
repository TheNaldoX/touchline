using System;
using System.Collections.Generic;
using System.Linq;
using Touchline.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Touchline
{
    // Recruitment centre: scouting department, recommendations, personality/interest lines and new signings.
    public sealed partial class TouchlineApp
    {
        static readonly string[] ScoutFocusLabels = { "Besoins de l’effectif", "Exploration d’un territoire", "Jeunes talents (≤ 21 ans)", "Joueurs libres", "Suivi de ma sélection" };
        static string ScoutFocusLabel(string focus) => ScoutFocusLabels[Math.Max(0, Array.IndexOf(Core.Career.ScoutFocuses, focus))];

        void ScoutingDepartmentPanel(VisualElement parent)
        {
            var scouts = Career.ScoutingDepartment(Database);
            var panel = Card(parent, "scout-department"); panel.name = "scout-department";
            Text(panel, "Cellule de recrutement", "section-title");
            Text(panel, scouts.Count + " / " + Career.ScoutSlots + " recruteur(s) pour votre budget · chaque recruteur affecté repère un profil toutes les deux semaines environ, deux observations à la fois.", "muted").name = "scout-department-summary";
            if (scouts.Count == 0) Text(panel, "Aucun recruteur en poste. Embauchez-en un dans Staff et délégation.", "notice");
            foreach (var s in scouts)
            {
                var card = new VisualElement(); card.AddToClassList("scout-member"); card.name = "scout-member-" + s.key; panel.Add(card);
                Text(card, s.member.name + (s.chief ? " · chef recruteur" : " · recruteur"), "recruit-hub-player-name");
                var bars = new VisualElement(); bars.AddToClassList("scout-member-bars"); card.Add(bars);
                ScoutSkillBar(bars, "Jugement du niveau", s.ability, "scout-skill-ability-" + s.key);
                ScoutSkillBar(bars, "Jugement du potentiel", s.potential, "scout-skill-potential-" + s.key);
                ScoutSkillBar(bars, "Adaptabilité", s.adaptability, "scout-skill-adaptability-" + s.key);
                Text(card, "Territoires connus : " + (s.regions.Count == 0 ? "aucun" : string.Join(" · ", s.regions.Take(4).Select(r => r.country + " " + r.level + " %"))), "footnote").name = "scout-regions-" + s.key;
                var a = s.assignment;
                var status = Text(card, a == null ? "Sans affectation : disponible pour une mission ou une affectation." : Core.Career.AssignmentLabel(a) + " · " + s.active + " / " + s.capacity + " observations · prochain profil vers le " + Core.Career.Epoch.AddDays(Math.Max(a.next, Career.life.day)).ToString("dd MMM", French) + " · " + a.reports + " profil(s) depuis le début", "scout-status");
                status.name = "scout-assignment-" + s.key;
                var actions = Row(card, "scout-actions");
                var assign = Button(actions, a == null ? "Affecter" : "Modifier", () => ScoutAssignmentDialog(s)); assign.name = "scout-assign-" + s.key; assign.AddToClassList("primary");
                if (a != null) Button(actions, "Retirer l’affectation", () => RunDecision(() => Career.ClearScoutAssignment(s.key))).name = "scout-unassign-" + s.key;
            }
            var footer = Row(panel, "scout-actions");
            Button(footer, "Embaucher / libérer", () => Navigate("Staff et délégation")).name = "scout-department-staff";
            Text(panel, "Qualités des recruteurs : jugement du niveau issu du staff ; potentiel et adaptabilité sont un profil de simulation stable. Les rapports d’affectation sont payés au lancement (+50 % à l’étranger).", "footnote");
        }
        void ScoutSkillBar(VisualElement parent, string label, int value, string name)
        {
            var row = Row(parent, "scout-range-row"); row.name = name; row.style.alignItems = Align.Center;
            Text(row, label + " " + value + " / 20", "scout-range-label");
            var track = new VisualElement(); track.style.flexGrow = 1; track.style.height = 10; track.style.minWidth = 80; track.style.backgroundColor = new Color(.5f, .5f, .55f, .25f); row.Add(track);
            var fill = new VisualElement(); fill.style.height = Length.Percent(100); fill.style.width = Length.Percent(Mathf.Clamp(value, 1, 20) * 5);
            fill.style.backgroundColor = value >= 15 ? new Color(.18f, .49f, .2f) : value >= 10 ? new Color(.25f, .6f, .95f) : new Color(.76f, .37f, 0); track.Add(fill);
        }
        void ScoutAssignmentDialog(ScoutProfile scout)
        {
            var panel = Modal("Affectation · " + scout.member.name); panel.name = "scout-assignment-dialog"; var body = Scroll(panel);
            var current = scout.assignment;
            Text(body, "Le recruteur cherche en continu dans ce cadre. Il choisit les profils selon son propre jugement : un recruteur moyen se trompe plus souvent.", "muted");
            var fields = Row(body, "scout-form");
            var focus = new DropdownField("Affectation", ScoutFocusLabels.ToList(), Math.Max(0, Array.IndexOf(Core.Career.ScoutFocuses, current?.focus ?? "needs"))) { name = "scout-assignment-focus" }; fields.Add(focus);
            var territories = ScoutingTerritories(); var country = new DropdownField("Territoire", territories, Math.Max(0, territories.IndexOf(current?.country ?? "Tous"))) { name = "scout-assignment-country" }; fields.Add(country);
            var role = new DropdownField("Poste", ScoutingRoles(), current?.role ?? "Tous") { name = "scout-assignment-role", formatListItemCallback = FrenchFootballPositions.Label, formatSelectedValueCallback = FrenchFootballPositions.Label }; fields.Add(role);
            var age = new IntegerField("Âge maximal") { value = current?.maxAge ?? 30, name = "scout-assignment-age" }; fields.Add(age);
            var hint = Text(body, "", "footnote"); hint.name = "scout-assignment-hint";
            void Hint()
            {
                string c = country.value; int known = c == "Tous" ? 100 : Career.RegionKnowledge(scout.key, c);
                hint.text = c == "Tous" ? "Tous territoires : il commence par les marchés qu’il connaît le mieux dans ses choix." : "Connaissance de " + c + " : " + known + " %" + (known < 50 ? " · territoire mal connu, recherches plus lentes et rapports plus larges au début." : ".");
            }
            country.RegisterValueChangedCallback(_ => Hint()); Hint();
            var confirm = Button(panel, "Confirmer l’affectation", () => RunDecision(() => { Career.AssignScout(Database, scout.key, Core.Career.ScoutFocuses[Math.Max(0, focus.index)], country.value, role.value, age.value); CloseModal(); }));
            confirm.name = "scout-assignment-confirm"; confirm.AddToClassList("primary");
        }

        void DepartmentRecommendationsPanel(VisualElement parent)
        {
            var ids = Career.DepartmentRecommendations(Database).Take(3).ToList();
            var section = Card(parent, "recruit-hub-section"); section.name = "recruit-hub-recommendations";
            Text(section, "Recommandations de la cellule", "section-title");
            if (ids.Count == 0) { Text(section, "Affectez vos recruteurs (onglet Missions) : les profils notés A ou B remonteront ici.", "recruit-hub-detail"); return; }
            var overview = CachedRecruitmentOverview();
            foreach (var id in ids)
            {
                var p = Database.Find(id); var card = Career.ScoutReportCard(Database, id, overview);
                var item = new VisualElement(); item.AddToClassList("recruit-hub-candidate"); item.name = "recruit-hub-recommendation-" + id; section.Add(item);
                var head = Row(item, "scout-report-head"); head.style.alignItems = Align.Center; GradeBadge(head, card.grade);
                Text(head, p.name + " · " + p.age + " ans · " + ClubName(p.team), "recruit-hub-player-name");
                Text(item, "Niveau " + ClubRatingScale.Range(card.ability,ClubRatingBaseline()) + (card.potential.known ? " · potentiel " + ClubRatingScale.Range(card.potential,ClubRatingBaseline()) : "") + (card.interest != null ? " · " + card.interest : ""), "recruit-hub-assessment");
                var actions = Row(item, "recruit-hub-actions");
                HubAction(actions, "Rapport", "recruit-hub-rec-report-" + id, () => { scoutReportFilter = "Tous"; recruitmentTab = "Rapports"; Build(); });
                HubAction(actions, "Agent", "recruit-hub-rec-agent-" + id, () => TransferDialog(id));
            }
        }

        void SigningsReviewPanel(VisualElement parent)
        {
            var reviews = Career.RecruitmentReview(Database).Take(4).ToList(); if (reviews.Count == 0) return;
            var section = Card(parent, "recruit-hub-section"); section.name = "recruit-hub-signings";
            Text(section, "Intégration des recrues", "section-title");
            Text(section, "Une recrue rend moins en match tant qu’elle s’adapte (pays, langue, adaptabilité). Le bilan compare le rapport au niveau réel constaté.", "recruit-hub-note");
            foreach (var r in reviews)
            {
                var p = Database.Find(r.player); var item = new VisualElement(); item.AddToClassList("recruit-hub-need"); item.name = "recruit-hub-signing-" + r.player; section.Add(item);
                var title = Row(item, "recruit-hub-need-title"); Text(title, p.name, "recruit-hub-player-name");
                var badge = Text(title, r.progress >= 1 ? "Intégré" : "Adaptation " + Mathf.RoundToInt(r.progress * 100) + " %", "recruit-hub-badge"); badge.EnableInClassList("recruit-hub-alert", r.progress < .5f);
                Text(item, r.verdict + (r.record.estimate > 0 ? " · estimé " + r.record.estimate.ToString("0") + ", constaté " + r.record.actual.ToString("0") : "") + (r.record.fromCountry != null ? " · arrivé de " + r.record.fromCountry : ""), "recruit-hub-detail");
            }
        }

        void PersonalityAndInterest(VisualElement card, PlayerData p, ScoutReportCardData data)
        {
            if (data.ambition.known || data.adaptability.known)
                Text(card, "Personnalité (profil simulé) : ambition " + data.ambition + " · adaptabilité " + data.adaptability + " / 20", "body-text").name = "scout-personality-" + p.id;
            else if (!data.exact) Text(card, "Personnalité : à découvrir (connaissance ≥ 65 %)", "footnote").name = "scout-personality-" + p.id;
            if (!string.IsNullOrEmpty(data.comparable)) Text(card, data.comparable, "body-text").name = "scout-comparable-" + p.id;
            if (!string.IsNullOrEmpty(data.interest)) Text(card, "Intérêt pour votre club : " + data.interest + (data.interestReasons.Count > 0 ? " · " + data.interestReasons[0] : ""), "scout-status").name = "scout-interest-" + p.id;
        }
    }
}
