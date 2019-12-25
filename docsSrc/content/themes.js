
var themes = {
    "light" : {
        "href" : "https://stackpath.bootstrapcdn.com/bootstrap/4.4.1/css/bootstrap.min.css",
        "integrity" : "sha384-Vkoo8x4CGsO3+Hhxv8T/Q5PaXtkKtu6ug5TOeNV6gBiFeWPGFN9MuhOf23Q9Ifjh",
        "button-text" : "Swap to Dark",
        "button-classes" : "btn btn-dark",
        "opposite-theme" : "dark"
    },
    "dark" : {
        "href" : "https://bootswatch.com/4/darkly/bootstrap.min.css",
        "integrity" : "sha384-Rq9MpH5hKzPKCxKgZouHt2sCwIFdGUK7fcffM75IhDQbxsRGIyisX5Ooi9E8ZrYR",
        "button-text" : "Swap to Light",
        "button-classes" : "btn btn-light",
        "opposite-theme" : "light"
    }
};

var themeStorageKey = 'theme';

function swapThemeInDom(theme) {
    var newTheme = themes[theme];
    var bootstrapCSS = document.getElementById('css-bootstrap');

    bootstrapCSS.setAttribute('integrity', newTheme['integrity']);
    bootstrapCSS.setAttribute('href', newTheme['href'])

}

function persistNewTheme(theme) {
    window.localStorage.setItem(themeStorageKey, theme);
}

function setToggleButton(theme) {
    var newTheme = themes[theme];
    var themeToggleButton = document.getElementById('theme-toggle');
    themeToggleButton.textContent = newTheme['button-text'];
    themeToggleButton.className = newTheme['button-classes'];
    themeToggleButton.onclick = function() {
        setTheme(newTheme['opposite-theme']);
    }
}

function setTheme(theme) {
    swapThemeInDom(theme);
    persistNewTheme(theme);
    try {
        setToggleButton(theme);
    }
    catch (e) {

    }

}

function loadTheme() {
    var theme = window.localStorage.getItem(themeStorageKey) || 'light';
    setTheme(theme);
}

document.addEventListener('DOMContentLoaded', (event) => {
    loadTheme()
});
loadTheme()
