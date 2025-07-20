// Wait for DOM to be fully loaded
document.addEventListener('DOMContentLoaded', function () {
    // Initialize when Swagger UI is ready
    const checkSwaggerReady = setInterval(() => {
        const swaggerUI = document.querySelector('.swagger-ui');
        const informationContainer = document.querySelector('.information-container .main');

        if (swaggerUI && informationContainer) {
            clearInterval(checkSwaggerReady);
            console.log('Swagger UI detected, initializing custom features...');

            // Initialize custom features with a small delay to ensure everything is loaded
            setTimeout(() => {
                initSearchAndFilters();
                initDarkModeToggle();
            }, 500);
        }
    }, 200);
});

// Initialize search and filter functionality
const initSearchAndFilters = () => {
    const informationContainer = document.querySelector('.information-container .main');

    // Create and style the search container
    const searchContainer = document.createElement('div');
    searchContainer.className = 'custom-search-container';
    searchContainer.style.margin = '20px 0';
    searchContainer.style.padding = '15px';
    searchContainer.style.backgroundColor = '#f8fafc';
    searchContainer.style.borderRadius = '8px';
    searchContainer.style.boxShadow = '0 2px 4px rgba(0, 0, 0, 0.1)';
    searchContainer.style.transition = 'background-color 0.3s ease';

    // Add heading
    const heading = document.createElement('h3');
    heading.textContent = 'API Explorer';
    heading.style.margin = '0 0 15px 0';
    heading.style.fontSize = '18px';
    heading.style.fontWeight = '700';

    // Create search input
    const searchWrapper = document.createElement('div');
    searchWrapper.style.position = 'relative';
    searchWrapper.style.marginBottom = '15px';

    const searchIcon = document.createElement('span');
    searchIcon.innerHTML = `
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="11" cy="11" r="8"></circle>
            <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
        </svg>
    `;
    searchIcon.style.position = 'absolute';
    searchIcon.style.left = '12px';
    searchIcon.style.top = '50%';
    searchIcon.style.transform = 'translateY(-50%)';
    searchIcon.style.color = '#64748b';

    const searchInput = document.createElement('input');
    searchInput.id = 'apiSearch';
    searchInput.type = 'text';
    searchInput.placeholder = 'Search APIs...';
    searchInput.style.width = '100%';
    searchInput.style.padding = '10px 10px 10px 40px';
    searchInput.style.border = '1px solid #e2e8f0';
    searchInput.style.borderRadius = '6px';
    searchInput.style.fontSize = '14px';
    searchInput.style.backgroundColor = '#ffffff';
    searchInput.style.transition = 'all 0.2s ease';
    searchInput.style.boxSizing = 'border-box';

    // Add focus and hover effects
    searchInput.addEventListener('focus', () => {
        searchInput.style.borderColor = '#3b82f6';
        searchInput.style.boxShadow = '0 0 0 3px rgba(59, 130, 246, 0.25)';
    });

    searchInput.addEventListener('blur', () => {
        searchInput.style.borderColor = '#e2e8f0';
        searchInput.style.boxShadow = 'none';
    });

    // Append search elements
    searchWrapper.appendChild(searchIcon);
    searchWrapper.appendChild(searchInput);

    // Create filter section
    const filterSection = document.createElement('div');
    filterSection.style.marginTop = '15px';

    const filterHeading = document.createElement('h4');
    filterHeading.textContent = 'Filter by Tag';
    filterHeading.style.margin = '0 0 10px 0';
    filterHeading.style.fontSize = '15px';
    filterHeading.style.fontWeight = '600';

    const checkboxContainer = document.createElement('div');
    checkboxContainer.id = 'checkboxContainer';
    checkboxContainer.style.display = 'flex';
    checkboxContainer.style.flexWrap = 'wrap';
    checkboxContainer.style.gap = '8px';

    // Add "Select All" checkbox
    const selectAllDiv = document.createElement('div');
    selectAllDiv.style.margin = '0 0 10px 0';
    selectAllDiv.style.width = '100%';

    const selectAllLabel = document.createElement('label');
    selectAllLabel.style.display = 'flex';
    selectAllLabel.style.alignItems = 'center';
    selectAllLabel.style.cursor = 'pointer';

    const selectAllCheckbox = document.createElement('input');
    selectAllCheckbox.type = 'checkbox';
    selectAllCheckbox.id = 'selectAll';
    selectAllCheckbox.style.margin = '0 8px 0 0';

    const selectAllText = document.createTextNode('Select All');
    selectAllLabel.appendChild(selectAllCheckbox);
    selectAllLabel.appendChild(selectAllText);
    selectAllDiv.appendChild(selectAllLabel);

    // Build the UI structure
    filterSection.appendChild(filterHeading);
    filterSection.appendChild(selectAllDiv);
    filterSection.appendChild(checkboxContainer);

    searchContainer.appendChild(heading);
    searchContainer.appendChild(searchWrapper);
    searchContainer.appendChild(filterSection);

    informationContainer.appendChild(searchContainer);

    // Populate checkboxes with unique tags
    populateTagCheckboxes(checkboxContainer, selectAllCheckbox);

    // Add event listeners
    searchInput.addEventListener('input', debounce(filterContent, 300));
    checkboxContainer.addEventListener('change', filterContent);
    selectAllCheckbox.addEventListener('change', toggleAllCheckboxes);

    // Update UI for dark mode syncing
    updateSearchUIForDarkMode();
};

// Populate tag checkboxes
const populateTagCheckboxes = (container, selectAllCheckbox) => {
    const tags = document.getElementsByClassName('opblock-tag-section');
    const uniqueTags = new Set();

    // Extract unique tags
    for (let i = 0; i < tags.length; i++) {
        const tagElement = tags[i].querySelector('[data-tag]');
        if (tagElement) {
            const tag = tagElement.getAttribute('data-tag');
            uniqueTags.add(tag);
        }
    }

    // Create checkbox for each tag
    uniqueTags.forEach(tag => {
        const checkboxDiv = document.createElement('div');
        checkboxDiv.style.backgroundColor = '#f1f5f9';
        checkboxDiv.style.padding = '6px 10px';
        checkboxDiv.style.borderRadius = '4px';
        checkboxDiv.style.display = 'inline-flex';
        checkboxDiv.style.alignItems = 'center';
        checkboxDiv.style.transition = 'background-color 0.2s ease';
        checkboxDiv.style.cursor = 'pointer';

        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.value = tag;
        checkbox.id = `tag-${tag}`;
        checkbox.style.margin = '0 8px 0 0';
        checkbox.checked = true; // Check all by default

        const label = document.createElement('label');
        label.htmlFor = `tag-${tag}`;
        label.textContent = tag;
        label.style.fontSize = '14px';
        label.style.cursor = 'pointer';
        label.style.userSelect = 'none';

        checkboxDiv.appendChild(checkbox);
        checkboxDiv.appendChild(label);
        container.appendChild(checkboxDiv);

        // Make the entire div clickable
        checkboxDiv.addEventListener('click', (e) => {
            if (e.target !== checkbox) {
                checkbox.checked = !checkbox.checked;
                checkbox.dispatchEvent(new Event('change', {bubbles: true}));
            }
        });
    });

    // Set "Select All" checkbox initial state
    selectAllCheckbox.checked = true;
};

// Toggle all checkboxes
const toggleAllCheckboxes = (event) => {
    const isChecked = event.target.checked;
    const checkboxes = document.querySelectorAll('#checkboxContainer input[type="checkbox"]');
    checkboxes.forEach(checkbox => {
        checkbox.checked = isChecked;
    });

    filterContent();
};

// Filter content based on search and checkboxes
const filterContent = () => {
    const searchInput = document.getElementById('apiSearch');
    const filter = searchInput.value.toLowerCase();
    const tagSections = document.getElementsByClassName('opblock-tag-section');

    // Get selected tags
    const checkedTags = Array.from(document.querySelectorAll('#checkboxContainer input[type="checkbox"]:checked'))
        .map(checkbox => checkbox.value.toLowerCase());

    let visibleEndpointCount = 0;

    // Update Select All checkbox state based on individual checkboxes
    const allCheckboxes = document.querySelectorAll('#checkboxContainer input[type="checkbox"]');
    const selectAllCheckbox = document.getElementById('selectAll');
    selectAllCheckbox.checked = allCheckboxes.length > 0 &&
        Array.from(allCheckboxes).every(checkbox => checkbox.checked);

    // Loop through each tag section
    for (let i = 0; i < tagSections.length; i++) {
        const tagSection = tagSections[i];
        const tagElement = tagSection.querySelector('[data-tag]');

        if (tagElement) {
            const tag = tagElement.getAttribute('data-tag').toLowerCase();
            const operations = tagSection.querySelectorAll('.opblock');

            // Check if tag is selected
            const tagMatches = checkedTags.includes(tag);
            let sectionHasVisibleOperations = false;

            // Filter operations within this tag
            operations.forEach(operation => {
                const pathElement = operation.querySelector('[data-path]');

                if (pathElement) {
                    const path = pathElement.getAttribute('data-path').toLowerCase();
                    const summary = operation.querySelector('.opblock-summary-description');
                    const summaryText = summary ? summary.textContent.toLowerCase() : '';

                    // Check if operation matches search and tag filter
                    const pathMatches = path.includes(filter);
                    const summaryMatches = summaryText.includes(filter);
                    const isVisible = tagMatches && (filter === '' || pathMatches || summaryMatches);

                    // Update visibility
                    operation.style.display = isVisible ? '' : 'none';

                    if (isVisible) {
                        sectionHasVisibleOperations = true;
                        visibleEndpointCount++;
                    }
                }
            });

            // Show/hide entire tag section
            tagSection.style.display = sectionHasVisibleOperations ? '' : 'none';
        }
    }

    // Show message if no results
    const noResultsMessage = document.getElementById('noResultsMessage') || createNoResultsMessage();

    if (visibleEndpointCount === 0 && filter !== '') {
        noResultsMessage.style.display = 'block';
        noResultsMessage.textContent = `No endpoints found matching "${filter}"`;
    } else {
        noResultsMessage.style.display = 'none';
    }
};

// Create "No Results" message element
const createNoResultsMessage = () => {
    const message = document.createElement('div');
    message.id = 'noResultsMessage';
    message.style.padding = '15px';
    message.style.textAlign = 'center';
    message.style.color = '#64748b';
    message.style.fontSize = '15px';
    message.style.margin = '20px 0';
    message.style.backgroundColor = '#f1f5f9';
    message.style.borderRadius = '6px';
    message.style.display = 'none';

    const firstOpblockTag = document.querySelector('.opblock-tag');
    if (firstOpblockTag && firstOpblockTag.parentElement) {
        firstOpblockTag.parentElement.insertBefore(message, firstOpblockTag);
    }

    return message;
};

// Initialize dark mode toggle
const initDarkModeToggle = () => {
    const authWrapper = document.querySelector('.auth-wrapper');

    if (!authWrapper) {
        console.error('Cannot find .auth-wrapper element to add the dark mode toggle.');
        return;
    }

    // Create toggle container
    const toggleDiv = document.createElement('div');
    toggleDiv.style.display = 'flex';
    toggleDiv.style.alignItems = 'center';
    toggleDiv.style.marginLeft = '15px';

    // Create toggle switch
    const toggleSwitch = document.createElement('label');
    toggleSwitch.className = 'dark-mode-switch';
    toggleSwitch.style.position = 'relative';
    toggleSwitch.style.display = 'inline-block';
    toggleSwitch.style.width = '44px';
    toggleSwitch.style.height = '24px';
    toggleSwitch.style.marginRight = '8px';

    // Create checkbox
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    checkbox.id = 'darkModeToggle';
    checkbox.style.opacity = '0';
    checkbox.style.width = '0';
    checkbox.style.height = '0';

    // Create slider
    const slider = document.createElement('span');
    slider.style.position = 'absolute';
    slider.style.cursor = 'pointer';
    slider.style.top = '0';
    slider.style.left = '0';
    slider.style.right = '0';
    slider.style.bottom = '0';
    slider.style.backgroundColor = '#cbd5e1';
    slider.style.borderRadius = '24px';
    slider.style.transition = '0.4s';

    // Create slider circle
    const sliderCircle = document.createElement('span');
    sliderCircle.style.position = 'absolute';
    sliderCircle.style.content = '""';
    sliderCircle.style.height = '18px';
    sliderCircle.style.width = '18px';
    sliderCircle.style.left = '3px';
    sliderCircle.style.bottom = '3px';
    sliderCircle.style.backgroundColor = 'white';
    sliderCircle.style.borderRadius = '50%';
    sliderCircle.style.transition = '0.4s';

    // Add icon for dark/light mode
    const icon = document.createElement('span');
    icon.innerHTML = '☀️';
    icon.style.position = 'absolute';
    icon.style.top = '50%';
    icon.style.left = '50%';
    icon.style.transform = 'translate(-50%, -50%)';
    icon.style.fontSize = '10px';

    // Create label text
    const labelText = document.createElement('span');
    labelText.textContent = 'Dark Mode';
    labelText.style.fontSize = '14px';
    labelText.style.fontWeight = '500';

    // Build the toggle structure
    slider.appendChild(sliderCircle);
    slider.appendChild(icon);
    toggleSwitch.appendChild(checkbox);
    toggleSwitch.appendChild(slider);
    toggleDiv.appendChild(toggleSwitch);
    toggleDiv.appendChild(labelText);

    // Add to DOM
    authWrapper.prepend(toggleDiv);

    // Load saved preference from localStorage
    const isDarkMode = localStorage.getItem('swaggerDarkMode') === 'true';
    checkbox.checked = isDarkMode;

    // Apply dark mode if saved
    if (isDarkMode) {
        document.querySelector('.swagger-ui').classList.add('dark-mode');
        sliderCircle.style.transform = 'translateX(20px)';
        icon.innerHTML = '🌙';
        updateSearchUIForDarkMode(true);
    }

    // Add event listener to toggle button
    checkbox.addEventListener('change', () => {
        const isDarkMode = checkbox.checked;
        const swaggerUI = document.querySelector('.swagger-ui');

        // Save preference to localStorage
        localStorage.setItem('swaggerDarkMode', isDarkMode);

        // Apply/remove dark mode class
        if (isDarkMode) {
            swaggerUI.classList.add('dark-mode');
            sliderCircle.style.transform = 'translateX(20px)';
            slider.style.backgroundColor = '#3b82f6';
            icon.innerHTML = '🌙';
        } else {
            swaggerUI.classList.remove('dark-mode');
            sliderCircle.style.transform = 'translateX(0)';
            slider.style.backgroundColor = '#cbd5e1';
            icon.innerHTML = '☀️';
        }

        // Update search UI for dark mode
        updateSearchUIForDarkMode(isDarkMode);
    });
};

// Update search UI for dark mode
const updateSearchUIForDarkMode = (isDarkMode = false) => {
    const searchContainer = document.querySelector('.custom-search-container');
    if (!searchContainer) return;

    if (isDarkMode || document.querySelector('.swagger-ui').classList.contains('dark-mode')) {
        searchContainer.style.backgroundColor = '#1e293b';
        searchContainer.style.color = '#f1f5f9';

        // Update search input
        const searchInput = document.getElementById('apiSearch');
        if (searchInput) {
            searchInput.style.backgroundColor = '#0f172a';
            searchInput.style.color = '#f1f5f9';
            searchInput.style.borderColor = '#334155';
        }

        // Update tag checkboxes
        const checkboxDivs = document.querySelectorAll('#checkboxContainer > div');
        checkboxDivs.forEach(div => {
            div.style.backgroundColor = '#334155';
            div.style.color = '#f1f5f9';
        });

        // Update no results message
        const noResultsMessage = document.getElementById('noResultsMessage');
        if (noResultsMessage) {
            noResultsMessage.style.backgroundColor = '#1e293b';
            noResultsMessage.style.color = '#94a3b8';
        }
    } else {
        searchContainer.style.backgroundColor = '#f8fafc';
        searchContainer.style.color = 'inherit';

        // Update search input
        const searchInput = document.getElementById('apiSearch');
        if (searchInput) {
            searchInput.style.backgroundColor = '#ffffff';
            searchInput.style.color = 'inherit';
            searchInput.style.borderColor = '#e2e8f0';
        }

        // Update tag checkboxes
        const checkboxDivs = document.querySelectorAll('#checkboxContainer > div');
        checkboxDivs.forEach(div => {
            div.style.backgroundColor = '#f1f5f9';
            div.style.color = 'inherit';
        });

        // Update no results message
        const noResultsMessage = document.getElementById('noResultsMessage');
        if (noResultsMessage) {
            noResultsMessage.style.backgroundColor = '#f1f5f9';
            noResultsMessage.style.color = '#64748b';
        }
    }
};

// Debounce function to limit how often a function is called
const debounce = (func, delay) => {
    let timeout;
    return function () {
        const context = this;
        const args = arguments;
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(context, args), delay);
    };
};