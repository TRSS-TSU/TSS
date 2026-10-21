const tabs = [...document.querySelectorAll('nav a')];
const sections = tabs.map(tab => document.querySelector(tab.getAttribute('href')));
const activate = id => tabs.forEach(tab => tab.classList.toggle('active', tab.getAttribute('href') === `#${id}`));
const observer = new IntersectionObserver(entries => entries.forEach(entry => { if (entry.isIntersecting) activate(entry.target.id); }), { rootMargin: '-35% 0px -55% 0px' });
sections.forEach(section => observer.observe(section));
document.getElementById('year').textContent = new Date().getFullYear();
