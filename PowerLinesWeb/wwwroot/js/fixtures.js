const $ = window.$

$('.selector').on('click', function () {
  const isSelected = $(this).hasClass('btn-primary')

  $('.selector').each(function () {
    $(this).removeClass('btn-primary')
    $(this).closest('tr').removeClass('success')
  })

  if (!isSelected) {
    $(this).addClass('btn-primary')
    $(this).closest('tr').addClass('success')
  }
})

$('.date-picker').on('click', function () {
  const selected = $(this).data('id')
  setView(selected)
  $(this).addClass('btn-primary')
})

$(function () {
  let selected = $('#today-id').first()

  if (selected === undefined) {
    selected = $('.date-picker').last()
  }

  setView(selected.data('id'))
  selected.addClass('btn-primary')
})

function setView (selected) {
  if (selected !== undefined) {
    $('.date-picker').each(function () {
      $(this).removeClass('btn-primary')
    })

    $('.odds-line').each(function () {
      if ($(this).data('id') !== selected) {
        $(this).hide()
      } else {
        $(this).fadeIn()
      }
    })
  }
}
