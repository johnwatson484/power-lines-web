const dateButtons = [...document.querySelectorAll('.date-picker')]
const filterButtons = [...document.querySelectorAll('.forecast-filter')]
const forecastRows = [...document.querySelectorAll('.forecast-row')]
const minimumProbability = document.getElementById('minimum-probability')
const order = document.getElementById('fixture-order')
const table = document.getElementById('fixture-table')
let selectedDate = document.querySelector('.date-picker[aria-pressed="true"]').dataset.date
let selectedFilter = 'all'
const selectedDivisions = new Set()
const selectedKickoffs = new Set()

function dayRows () {
  return forecastRows.filter(row => row.dataset.date === selectedDate)
}

function buildOptions (kind, selections, options) {
  const container = document.getElementById(`${kind}-options`)
  container.querySelectorAll('label').forEach(label => label.remove())
  for (const [value, text] of options) {
    const label = document.createElement('label')
    label.className = 'fixture-option'
    const input = document.createElement('input')
    input.type = 'checkbox'
    input.value = value
    input.addEventListener('change', () => {
      if (input.checked) selections.add(value)
      else selections.delete(value)
      updateFixtures()
    })
    label.append(input, document.createTextNode(text))
    container.append(label)
  }
}

function rebuildDayOptions () {
  const rows = dayRows()
  const divisions = new Map(rows.map(row => [row.dataset.division, row.dataset.divisionLabel]))
  const kickoffs = new Set(rows.map(row => row.dataset.kickoff))
  buildOptions('division', selectedDivisions, [...divisions].sort((a, b) => a[1].localeCompare(b[1])))
  buildOptions('kickoff', selectedKickoffs, [...kickoffs].sort().map(time => [time, time === 'unverified' ? 'Time unverified' : time]))
}

function updateForecastButtons () {
  filterButtons.forEach(button => {
    const selected = button.dataset.filter === selectedFilter
    button.classList.toggle('btn-secondary', selected)
    button.classList.toggle('btn-outline-secondary', !selected)
    button.setAttribute('aria-pressed', String(selected))
  })
}

// Compare exact decimal strings rather than rounding the server's decimal values
// to JavaScript Numbers. Percentage inputs also accept native exponential notation.
function probabilityKey (value, percentage = false) {
  const [coefficient, exponent = '0'] = value.toLowerCase().split('e')
  const [whole, fraction = ''] = coefficient.replace(/^[+-]/, '').split('.')
  const digits = (whole + fraction).replace(/^0+/, '')
  const point = digits.length - fraction.length + Number(exponent) - (percentage ? 2 : 0)
  const zero = `0.${'0'.repeat(28)}`
  if (!digits) return zero
  // A positive threshold below decimal precision still excludes exactly zero.
  if (point < -28) return `${zero}1`
  const expanded = point === 1 ? `1.${digits.slice(1)}` : `0.${'0'.repeat(-point)}${digits}`
  const [integer, decimals] = expanded.split('.')
  return `${integer}.${decimals.replace(/0+$/, '').padEnd(28, '0')}`
}

function updateFixtures () {
  const validMinimum = minimumProbability.validity.valid
  const minimum = minimumProbability.value === '' || !validMinimum ? null : probabilityKey(minimumProbability.value, true)
  minimumProbability.setAttribute('aria-invalid', String(!validMinimum))
  document.getElementById('probability-error').hidden = validMinimum
  let visibleCount = 0
  const rows = dayRows()
  forecastRows.forEach(row => {
    const { tier, price, division, kickoff, probability, date } = row.dataset
    const matchesFilter = selectedFilter === 'all' ||
      (selectedFilter === 'shortlist' && tier !== 'none') ||
      selectedFilter === tier ||
      (selectedFilter === 'price' && price === 'true')
    const matchesMinimum = minimum === null || (probability !== '' && probabilityKey(probability) >= minimum)
    row.hidden = date !== selectedDate || !matchesFilter || !matchesMinimum ||
      (selectedDivisions.size > 0 && !selectedDivisions.has(division)) ||
      (selectedKickoffs.size > 0 && !selectedKickoffs.has(kickoff))
    if (!row.hidden) visibleCount++
  })

  rows.sort((a, b) => {
    const aProbability = a.dataset.probability
    const bProbability = b.dataset.probability
    if ((aProbability === '') !== (bProbability === '')) return aProbability === '' ? 1 : -1
    const probabilityOrder = probabilityKey(bProbability).localeCompare(probabilityKey(aProbability))
    if (order.value === 'probability' && probabilityOrder !== 0) return probabilityOrder
    return a.dataset.kickoffOrder.localeCompare(b.dataset.kickoffOrder) || Number(a.dataset.fixtureId) - Number(b.dataset.fixtureId)
  })
  const body = table.tBodies[0]
  rows.forEach(row => body.append(row))

  const divisionLabels = new Map(rows.map(row => [row.dataset.division, row.dataset.divisionLabel]))
  const criteria = []
  if (selectedDivisions.size) criteria.push(`Divisions: ${[...selectedDivisions].map(value => divisionLabels.get(value)).join(', ')}`)
  if (selectedKickoffs.size) criteria.push(`Kickoff: ${[...selectedKickoffs].map(value => value === 'unverified' ? 'Time unverified' : value).join(', ')}`)
  if (minimum !== null) criteria.push(`Minimum win probability: ${minimumProbability.value}%`)
  if (selectedFilter !== 'all') criteria.push(filterButtons.find(button => button.dataset.filter === selectedFilter).textContent)
  document.getElementById('active-criteria').textContent = criteria.length ? criteria.join(' · ') : 'All divisions · All kickoff times · Any win probability · All fixtures'
  document.getElementById('division-summary').textContent = `Divisions: ${selectedDivisions.size || 'All'}`
  document.getElementById('kickoff-summary').textContent = `Kickoff times: ${selectedKickoffs.size || 'All'}`
  document.getElementById('fixture-count').textContent = `Showing ${visibleCount} of ${rows.length} fixtures`
  document.getElementById('no-fixtures').hidden = visibleCount > 0
  table.hidden = visibleCount === 0
}

dateButtons.forEach(button => {
  button.addEventListener('click', () => {
    selectedDate = button.dataset.date
    selectedDivisions.clear()
    selectedKickoffs.clear()
    dateButtons.forEach(day => {
      const selected = day === button
      day.classList.toggle('btn-primary', selected)
      day.classList.toggle('btn-default', !selected)
      day.setAttribute('aria-pressed', String(selected))
    })
    rebuildDayOptions()
    updateFixtures()
  })
})

filterButtons.forEach(button => {
  button.addEventListener('click', () => {
    selectedFilter = button.dataset.filter
    updateForecastButtons()
    updateFixtures()
  })
})

minimumProbability.addEventListener('input', updateFixtures)
order.addEventListener('change', updateFixtures)
document.getElementById('clear-filters').addEventListener('click', () => {
  selectedDivisions.clear()
  selectedKickoffs.clear()
  minimumProbability.value = ''
  selectedFilter = 'all'
  updateForecastButtons()
  rebuildDayOptions()
  updateFixtures()
})

rebuildDayOptions()
updateFixtures()
document.getElementById('fixture-controls').hidden = false
