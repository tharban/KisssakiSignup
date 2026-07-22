(() => {
    const categories = [
        [1, "Without Bogu"],
        [2, "Age 7 to 9"],
        [3, "Age 10 to 12"],
        [4, "Age 13 to 15"],
        [5, "Age 16 to 18"],
        [6, "Adult Kyu"]
    ];
    const state = {
        club: { name: "", city: "", country: "", address: "", email: "", phone: "", web: "" },
        contact: { name: "", email: "", phone: "", notes: "" },
        competitors: [],
        teams: []
    };
    let currentStep = 0;

    if (window.initialRegistrationPayload) {
        Object.assign(state.club, window.initialRegistrationPayload.club || {});
        Object.assign(state.contact, window.initialRegistrationPayload.contact || {});
        state.competitors = window.initialRegistrationPayload.competitors || [];
        state.teams = (window.initialRegistrationPayload.teams || []).map((team) => ({
            ...team,
            members: [1, 2, 3].map((position) =>
                (team.members || []).find((member) => Number(member.position) === position) || { position, competitorClientId: "" })
        }));
    }

    const escapeHtml = (value) => String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
    const createClientId = () => window.crypto?.randomUUID?.() || `competitor-${Date.now()}-${Math.random()}`;
    const form = document.getElementById("registration-form");
    const competitorsElement = document.getElementById("competitors");
    const teamsElement = document.getElementById("teams");
    const reviewElement = document.getElementById("review");
    const steps = Array.from(document.querySelectorAll("[data-step]"));
    const previousButton = document.getElementById("previous-step");
    const nextButton = document.getElementById("next-step");
    const submitButton = document.getElementById("submit-registration");

    document.querySelectorAll("[data-field]").forEach((field) => {
        const [section, property] = field.dataset.field.split(".");
        field.value = state[section][property] || "";
        field.addEventListener("input", () => {
            state[section][property] = field.value;
        });
    });

    const renderCompetitors = () => {
        competitorsElement.innerHTML = state.competitors.map((competitor, index) => {
            const selectedCategories = new Set(competitor.categories || []);
            const categoryInputs = categories.map(([value, label]) => `
                <label><input type="checkbox" data-category="${value}" ${selectedCategories.has(value) ? "checked" : ""} /> ${label}</label>`).join("");
            return `
                <article class="entry" data-competitor-index="${index}">
                    <h3>Teilnehmer ${index + 1}</h3>
                    <label>Vorname<input data-property="firstName" value="${escapeHtml(competitor.firstName)}" type="text" /></label>
                    <label>Nachname<input data-property="lastName" value="${escapeHtml(competitor.lastName)}" type="text" /></label>
                    <label>Ausweisnummer<input data-property="idCard" value="${escapeHtml(competitor.idCard)}" type="text" /></label>
                    <label>Geburtsjahr<input data-property="birthYear" value="${escapeHtml(competitor.birthYear)}" type="number" min="1900" max="2100" /></label>
                    <label>Graduierung<input data-property="rankText" value="${escapeHtml(competitor.rankText)}" type="text" /></label>
                    <label><input data-property="hasBogu" type="checkbox" ${competitor.hasBogu ? "checked" : ""} /> Bogu vorhanden</label>
                    <label>Hinweise<textarea data-property="notes" rows="2">${escapeHtml(competitor.notes)}</textarea></label>
                    <fieldset><legend>Kategorien</legend>${categoryInputs}</fieldset>
                    <button type="button" data-remove-competitor="${index}">Entfernen</button>
                </article>`;
        }).join("");

        competitorsElement.querySelectorAll("[data-competitor-index]").forEach((entry) => {
            const competitor = state.competitors[Number(entry.dataset.competitorIndex)];
            entry.querySelectorAll("[data-property]").forEach((field) => {
                const property = field.dataset.property;
                const update = () => {
                    competitor[property] = field.type === "checkbox" ? field.checked : field.value;
                    if (property === "birthYear") competitor[property] = Number(field.value) || 0;
                    renderTeams();
                };
                field.addEventListener("input", update);
                field.addEventListener("change", update);
            });
            entry.querySelectorAll("[data-category]").forEach((field) => field.addEventListener("change", () => {
                competitor.categories = Array.from(entry.querySelectorAll("[data-category]:checked"), (selected) => Number(selected.dataset.category));
            }));
        });
        competitorsElement.querySelectorAll("[data-remove-competitor]").forEach((button) => button.addEventListener("click", () => {
            state.competitors.splice(Number(button.dataset.removeCompetitor), 1);
            state.teams.forEach((team) => team.members.forEach((member) => {
                if (!state.competitors.some((competitor) => competitor.clientId === member.competitorClientId)) member.competitorClientId = "";
            }));
            renderCompetitors();
            renderTeams();
        }));
    };

    const renderTeams = () => {
        const competitorOptions = (selectedClientId) => ["<option value=\"\">Bitte waehlen</option>"]
            .concat(state.competitors.map((competitor) => `<option value="${escapeHtml(competitor.clientId)}" ${competitor.clientId === selectedClientId ? "selected" : ""}>${escapeHtml(`${competitor.firstName} ${competitor.lastName}`.trim() || competitor.clientId)}</option>`))
            .join("");
        teamsElement.innerHTML = state.teams.map((team, index) => `
            <article class="entry" data-team-index="${index}">
                <h3>Team ${index + 1}</h3>
                <label>Name<input data-team-property="name" value="${escapeHtml(team.name)}" type="text" /></label>
                <label>Teamklasse
                    <select data-team-property="teamType">
                        <option value="1" ${Number(team.teamType) === 1 ? "selected" : ""}>Jugend</option>
                        <option value="2" ${Number(team.teamType) === 2 ? "selected" : ""}>Erwachsene</option>
                    </select>
                </label>
                ${[1, 2, 3].map((position) => {
                    const member = (team.members || []).find((candidate) => Number(candidate.position) === position) || { position, competitorClientId: "" };
                    return `<label>Position ${position}<select data-team-position="${position}">${competitorOptions(member.competitorClientId)}</select></label>`;
                }).join("")}
                <button type="button" data-remove-team="${index}">Entfernen</button>
            </article>`).join("");

        teamsElement.querySelectorAll("[data-team-index]").forEach((entry) => {
            const team = state.teams[Number(entry.dataset.teamIndex)];
            entry.querySelectorAll("[data-team-property]").forEach((field) => field.addEventListener("change", () => {
                team[field.dataset.teamProperty] = field.dataset.teamProperty === "teamType" ? Number(field.value) : field.value;
            }));
            entry.querySelectorAll("[data-team-position]").forEach((field) => field.addEventListener("change", () => {
                const position = Number(field.dataset.teamPosition);
                const member = team.members.find((candidate) => Number(candidate.position) === position);
                member.competitorClientId = field.value;
            }));
        });
        teamsElement.querySelectorAll("[data-remove-team]").forEach((button) => button.addEventListener("click", () => {
            state.teams.splice(Number(button.dataset.removeTeam), 1);
            renderTeams();
        }));
    };

    const renderReview = () => {
        const club = escapeHtml(state.club.name || "-");
        const contact = escapeHtml(state.contact.name || "-");
        const competitorNames = state.competitors.map((competitor) => `<li>${escapeHtml(`${competitor.firstName} ${competitor.lastName}`.trim() || "Unbenannt")}</li>`).join("") || "<li>Keine Teilnehmer</li>";
        const teamNames = state.teams.map((team) => `<li>${escapeHtml(team.name || "Unbenannt")}</li>`).join("") || "<li>Keine Teams</li>";
        reviewElement.innerHTML = `<p><strong>Verein:</strong> ${club}</p><p><strong>Kontakt:</strong> ${contact}</p><h3>Teilnehmer</h3><ul>${competitorNames}</ul><h3>Teams</h3><ul>${teamNames}</ul>`;
    };

    const showStep = (step) => {
        currentStep = step;
        steps.forEach((element, index) => { element.hidden = index !== currentStep; });
        previousButton.hidden = currentStep === 0;
        nextButton.hidden = currentStep === steps.length - 1;
        submitButton.hidden = currentStep !== steps.length - 1;
        if (currentStep === steps.length - 1) renderReview();
    };

    document.getElementById("add-competitor").addEventListener("click", () => {
        state.competitors.push({ clientId: createClientId(), firstName: "", lastName: "", idCard: "", birthYear: 0, rankText: "", hasBogu: false, notes: "", categories: [] });
        renderCompetitors();
        renderTeams();
    });
    document.getElementById("add-team").addEventListener("click", () => {
        state.teams.push({ name: "", teamType: 1, members: [{ position: 1, competitorClientId: "" }, { position: 2, competitorClientId: "" }, { position: 3, competitorClientId: "" }] });
        renderTeams();
    });
    previousButton.addEventListener("click", () => showStep(Math.max(0, currentStep - 1)));
    nextButton.addEventListener("click", () => showStep(Math.min(steps.length - 1, currentStep + 1)));
    form.addEventListener("submit", () => {
        document.getElementById("payload-json").value = JSON.stringify(state);
    });

    renderCompetitors();
    renderTeams();
    showStep(0);
})();
