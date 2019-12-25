
var themes = {
    "light" : {
        "href" : "https://raw.githubusercontent.com/ForEvolve/bootstrap-dark/master/dist/css/toggle-bootstrap.min.css",
        "integrity" : "sha384-Vkoo8x4CGsO3+Hhxv8T/Q5PaXtkKtu6ug5TOeNV6gBiFeWPGFN9MuhOf23Q9Ifjh",
        "button-text" : "Swap to Dark",
        "button-classes" : "btn btn-dark",
        "opposite-theme" : "dark",
        "boos" : "bootstrap"
    },
    "dark" : {
        "href" : "https://bootswatch.com/4/darkly/bootstrap.min.css",
        "integrity" : "sha384-Rq9MpH5hKzPKCxKgZouHt2sCwIFdGUK7fcffM75IhDQbxsRGIyisX5Ooi9E8ZrYR",
        "button-text" : "Swap to Light",
        "button-classes" : "btn btn-light",
        "opposite-theme" : "light",
        "boos" : "bootstrap-dark"
    }
};

var themeStorageKey = 'theme';

function swapThemeInDom(theme) {
    var newTheme = themes[theme];
    var bootstrapCSS = document.getElementsByTagName('body')[0];

    // bootstrapCSS.setAttribute('integrity', newTheme['integrity']);
    bootstrapCSS.setAttribute('class', newTheme['boos'])

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

document.addEventListener('readystatechange', (event) => {
    loadTheme()
});
// loadTheme()
