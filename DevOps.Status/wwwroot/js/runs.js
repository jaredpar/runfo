$(document).ready(function () {  
    $('#search-button').click(function(e) {
        e.preventDefault();
        var button = $(this)
        button.prop('disabled', true);
        var definition = $('#search-definition').val();
        var query = $('#search-query').val();
        $.ajax({  
            type: "GET",  
            url: "/api/runfo/jobs/" + definition + "?query=" + query,  
            contentType: "application/json",
            dataType: "json",  
            success: function (data) {  
                var ctx = document.getElementById('search-chart').getContext('2d');
                data.sort((l, r) => -(l.failed - r.failed));
                let labels = data.map(x => x.jobName);
                let values = data.map(x => x.failed);
                var myBarChart = new Chart(ctx, {
                    type: 'horizontalBar',
                    data: {
                        labels: labels,
                        datasets: [{
                            label: 'Fail Count',
                            data: values,
                        }]
                    },
                    options: {
                        scales: {
                            yAxes: [{
                                ticks: {
                                    beginAtZero: true
                                }
                            }]
                        }
                    }
                });
                button.prop('disabled', false);
            },
            failure: function (data) {  
                alert(data.responseText);  
                button.prop('disabled', false);
            },
            error: function (data) {  
                alert(data.responseText);  
                button.prop('disabled', false);
            }
        });
    });  
});