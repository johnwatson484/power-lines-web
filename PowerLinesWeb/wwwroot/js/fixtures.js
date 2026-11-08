const dateButtons = document.querySelectorAll('.date-picker')
const filterButtons = document.querySelectorAll('.forecast-filter')
const forecastRows = document.querySelectorAll('.forecast-row')
let selectedDate = document.querySelector('.date-picker[aria-pressed="true"]').dataset.date
let selectedFilter = 'all'

function updateFixtures () {
  let visibleCount = 0
  forecastRows.forEach(row => {
    const tier = row.dataset.tier
    const matchesFilter = selectedFilter === 'all' ||
      (selectedFilter === 'shortlist' && tier !== 'none') ||
      selectedFilter === tier ||
      (selectedFilter === 'price' && row.dataset.price === 'true')
    row.hidden = row.dataset.date !== selectedDate || !matchesFilter
    if (!row.hidden) visibleCount++
  })
  document.getElementById('no-fixtures').hidden = visibleCount > 0
  document.getElementById('fixture-table').hidden = visibleCount === 0
}

dateButtons.forEach(button => {
  button.addEventListener('click', () => {
    selectedDate = button.dataset.date
    dateButtons.forEach(day => {
      const selected = day === button
      day.classList.toggle('btn-primary', selected)
      day.classList.toggle('btn-default', !selected)
      day.setAttribute('aria-pressed', String(selected))
    })
    updateFixtures()
  })
})

filterButtons.forEach(button => {
  button.addEventListener('click', () => {
    selectedFilter = button.dataset.filter
    filterButtons.forEach(filter => {
      const selected = filter === button
      filter.classList.toggle('btn-secondary', selected)
      filter.classList.toggle('btn-outline-secondary', !selected)
      filter.setAttribute('aria-pressed', String(selected))
    })
    updateFixtures()
  })
})

updateFixtures()
