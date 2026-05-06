define(["loading", "toast"], function (loading, toast) {
  "use strict";

  var BL_ID = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

  function showStatus(msg, type) {
    var el = document.getElementById("bl_status");
    if (!el) return;
    el.textContent = msg;
    el.style.display = "block";
    el.style.background =
      type === "ok" ? "#1a3a1a" : type === "err" ? "#3a1a1a" : "#1a2a3a";
    el.style.color =
      type === "ok" ? "#7bc47b" : type === "err" ? "#c47b7b" : "#7babc4";
  }

  function loadConfig() {
    loading.show();
    ApiClient.getPluginConfiguration(BL_ID).then(function (c) {
      document.getElementById("bl_key").value = c.ApiKey || "";
      document.getElementById("bl_url").value = c.BenchlibApiUrl || "";
      document.getElementById("bl_movies").checked = c.EnableMovies !== false;
      document.getElementById("bl_series").checked = c.EnableSeries !== false;
      document.getElementById("bl_music").checked = c.EnableMusic !== false;
      loading.hide();
    });
  }

  function saveConfig() {
    ApiClient.getPluginConfiguration(BL_ID).then(function (c) {
      c.ApiKey = document.getElementById("bl_key").value.trim();
      c.BenchlibApiUrl =
        document.getElementById("bl_url").value.trim() ||
        "https://api.benchlib.com";
      c.EnableMovies = document.getElementById("bl_movies").checked;
      c.EnableSeries = document.getElementById("bl_series").checked;
      c.EnableMusic = document.getElementById("bl_music").checked;
      ApiClient.updatePluginConfiguration(BL_ID, c).then(function () {
        Dashboard.processPluginConfigurationUpdateResult();
        showStatus("Enregistré ✓", "ok");
      });
    });
  }

  function testConnection() {
    var key = document.getElementById("bl_key").value.trim();
    var url = (
      document.getElementById("bl_url").value.trim() ||
      "https://api.benchlib.com"
    ).replace(/\/$/, "");
    if (!key) {
      showStatus("Renseignez votre clé API.", "err");
      return;
    }
    showStatus("Test en cours...", "info");
    var x = new XMLHttpRequest();
    x.open("GET", url + "/api/v1/ping", true);
    x.setRequestHeader("X-BenchLib-Key", key);
    x.timeout = 10000;
    x.onload = function () {
      x.status === 200
        ? showStatus("Connexion réussie ✓", "ok")
        : x.status === 401
          ? showStatus("Clé API invalide.", "err")
          : showStatus("Erreur " + x.status, "err");
    };
    x.onerror = function () {
      showStatus("Impossible de joindre l'API.", "err");
    };
    x.ontimeout = function () {
      showStatus("Timeout.", "err");
    };
    x.send();
  }

  document
    .querySelector("#BenchlibConfigPage")
    .addEventListener("viewshow", function () {
      loadConfig();
      document.getElementById("bl_save").addEventListener("click", saveConfig);
      document
        .getElementById("bl_test")
        .addEventListener("click", testConnection);
    });
});
