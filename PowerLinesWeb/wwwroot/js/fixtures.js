const $ = window.$
$('.forecast-filter').on('click', function () {
  const filter = $(this).data('filter')
  $('.forecast-filter').removeClass('btn-secondary').addClass('btn-outline-secondary')
  $(this).addClass('btn-secondary').removeClass('btn-outline-secondary')
  $('.forecast-row').each(function () {
    const tier = $(this).data('tier')
    $(this).toggle(filter === 'all' || (filter === 'shortlist' && tier !== 'none') || filter === tier || (filter === 'price' && $(this).data('price') === true))
  })
})
