$(document).ready(function () {  
    $('#search-button').click(function(e) {
        e.preventDefault();
        var button = $(this)
        button.prop('disabled', true);
        var query = $('#search-text').val();
        $.ajax({  
            type: "GET",  
            url: "/api/runfo/builds?query=" + query,  
            contentType: "application/json",
            dataType: "json",  
            success: function (data) {  
                var body = $('#table-body');
                body.empty();

                var total = 0;
                $.each(data, function (i, item) {  
                    var row = '<tr>' +
                        '<td>' + item.result + '</td>' +
                        '<td><a href="' + item.buildUri + '">' + item.buildNumber + '</td>' +
                        '<tr>';
                    body.append(row);
                    total++;
                });

                var summary = $('#search-summary');
                summary.empty();
                summary.append(total + " results returned");
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