
var themes = {
    "light" : {
        "button-text" : "Swap to Dark",
        "button-classes" : "btn btn-dark",
        "opposite-theme" : "dark",
        "body-class" : "bootstrap"
    },
    "dark" : {
        "button-text" : "Swap to Light",
        "button-classes" : "btn btn-light",
        "opposite-theme" : "light",
        "body-class" : "bootstrap-dark"
    }
};

var themeStorageKey = 'theme';

function swapThemeInDom(theme) {
    var newTheme = themes[theme];
    var bootstrapCSS = document.getElementsByTagName('body')[0];
    bootstrapCSS.setAttribute('class', newTheme['body-class'])
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
    try {
        swapThemeInDom(theme);
    }
    catch(e){

    }
    try {
    persistNewTheme(theme);
    }
    catch(e) {

    }
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
